using DigitalCap.Core.Constants;
using DigitalCap.Core.Helpers.Extensions;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Reporting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Cors.Infrastructure;
using NPOI.XSSF.Streaming.Values;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DigitalCap.Infrastructure.Service
{
    using TemplateConstants = DigitalCap.Core.Helpers.Constants.Template;
    using Template = DigitalCap.Core.Models.Reporting.Template;
    public class TemplateService : ITemplateService
    {
        private readonly ITemplateRepository _templateRepository;
        private readonly IProjectService _projectService;
        private readonly IProjectRepository _projectRepository;
        private readonly IVesselService _vesselService;
        private readonly IFileStorageRepository _fileStorageRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly ITaskService _taskService;
        private readonly IPortRepository _portRepository;
        private class SectionDocument
        {
            public TemplateSection Section { get; set; }
            public byte[] Data { get; set; }
        }
        public TemplateService(ITemplateRepository templateRepository, IProjectService projectService,
            IVesselService vesselService, IProjectRepository projectRepository,
            IFileStorageRepository fileStorageRepository, ITaskService taskService, ITaskRepository taskRepository, IPortRepository portRepository)
        {
            _templateRepository = templateRepository;
            _projectService = projectService;
            _vesselService = vesselService;
            _projectRepository = projectRepository;
            _fileStorageRepository = fileStorageRepository;
            _taskService = taskService;
            _taskRepository = taskRepository;
            _portRepository = portRepository;
        }
        //public async Task<byte[]> PopulateTemplate(int projectId)
        //{
        //    var project = await _projectRepository.GetProject(projectId);

        //    if (project == null)
        //        throw new ApplicationException($"No project found for id {projectId}");

        //    var template = await ResolveTemplate(project);

        //    if (template == null)
        //        throw new ApplicationException($"No Template found for project id {project.Id}");

        //    var templateParts = GetTemplateParts(template);
        //    var populatedTemplateParts = await ReplaceTokens(templateParts, project);
        //    var joinedTemplate = JoinDocuments(populatedTemplateParts);

        //    return joinedTemplate;
        //}

        //private async Task<Template> ResolveTemplate(Project project)
        //{
        //    int? templateId = null;

        //    var template = templateId != null
        //                    ? await _templateRepository.GetTemplate(templateId.Value)
        //                    : null;
        //    return template;
        //}
        private IEnumerable<Task<SectionDocument>> GetTemplateParts(Template template)
        {
            if (template?.Sections == null)
                return Array.Empty<Task<SectionDocument>>();

            var contentCache = new Dictionary<int, byte[]>();
            var fileSections = template.Sections.Where(x => x.File != null);
            var fileSectionTasks = fileSections.Select(currentSection =>
                Task.Run(() =>
                {
                    if (!contentCache.ContainsKey(currentSection.File.Id))
                    {
                        lock (contentCache)
                        {
                            if (!contentCache.ContainsKey(currentSection.File.Id))
                            {
                                contentCache.Add(currentSection.File.Id, _fileStorageRepository.GetFileContent(currentSection.File.Id).Result);
                            }
                        }
                    }
                    var content = contentCache[currentSection.File.Id]; ;

                    if ((content?.Length ?? 0) == 0)
                        return null;

                    return new SectionDocument
                    {
                        Section = currentSection,
                        Data = content
                    };
                }));

            return fileSectionTasks;
        }

      //  private async Task<IEnumerable<SectionDocument>> ReplaceTokens(
      //IEnumerable<Task<SectionDocument>> source,
      //Project project)
      //  {
      //      var taskCache = new TaskCache(_taskRepository);

      //      var vesselResult = project.IMO.HasValue
      //          ? await _vesselService.Details(project.IMO.Value.ToString())
      //          : null;

      //      var vessel = vesselResult?.Data;

      //      var populateDocumentTasks = source.Select(async x =>
      //          await PopulateDocument(await x, project, taskCache, vessel));

      //      var result = await Task.WhenAll(populateDocumentTasks);

      //      return result;
      //  }
        private byte[] JoinDocuments(IEnumerable<SectionDocument> partDocuments)
        {
            var orderedParts = partDocuments.OrderBy(x => x.Section.Sequence ?? int.MaxValue).ToList();
            var hostPart = orderedParts.First();
            var childParts = orderedParts.Skip(1);
            var subDocuments = new ConcurrentDictionary<string, byte[]>();

            // Add subdocuments
            using (var hostStream = new MemoryStream())
            {
                hostStream.Write(hostPart.Data, 0, hostPart.Data.Length);

                using (var document = WordprocessingDocument.Open(hostStream, true))
                {
                    foreach (var currentChild in childParts)
                    {
                        var childName = AddSubdocument(document, currentChild);

                        subDocuments.AddOrUpdate(
                            key: childName,
                            addValue: currentChild.Data,
                            (key, value) => currentChild.Data);
                    }
                }

                hostPart.Data = hostStream.ToArray();
            }

            using (var archiveStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
                {
                    // Add master document entry
                    var hostEntry = archive.CreateEntry("CAP.docx", CompressionLevel.Fastest);

                    using (var hostEntryStream = hostEntry.Open())
                        hostEntryStream.Write(hostPart.Data, 0, hostPart.Data.Length);

                    // Add subdocument entries
                    foreach (var currentChild in subDocuments)
                    {
                        var entry = archive.CreateEntry(currentChild.Key, CompressionLevel.Fastest);

                        using (var childEntryStream = entry.Open())
                            childEntryStream.Write(currentChild.Value, 0, currentChild.Value.Length);
                    }
                }

                var resultData = archiveStream.ToArray();

                return resultData;
            }
        }

        private string AddSubdocument(WordprocessingDocument hostDocument, SectionDocument currentChild)
        {
            const string childRootPath = "Sections";
            var validName = currentChild.Section.Name.ToValidFileName();
            var fileName = $@"{childRootPath}\{validName}.docx";

            var hostMainPart = hostDocument.MainDocumentPart;
            var hostBody = hostMainPart.Document.Body;

            var relationship = hostMainPart.AddExternalRelationship(
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/subDocument",
                new Uri(fileName, UriKind.Relative));

            var childReference = new SubDocumentReference
            {
                Id = new DocumentFormat.OpenXml.StringValue(relationship.Id)
            };

            var paragraphSettings = new ParagraphProperties
            {
                PageBreakBefore = new PageBreakBefore { Val = new OnOffValue(true) }
            };

            var childReferenceParagraph = new Paragraph(paragraphSettings, childReference);
            hostBody.Append(childReferenceParagraph);

            return fileName;
        }

        //private async Task<SectionDocument> PopulateDocument(SectionDocument sectionDocument, Project project, TaskCache taskCache, Vessel vessel)
        //{
        //    var gradingSection = await _templateRepository.GetTemplateGradingSection(sectionDocument.Section);
        //    var replacements = GetReplacements(project, taskCache, vessel, sectionDocument.Section, gradingSection);
        //    sectionDocument.Data = PopulateDocument(sectionDocument.Data, replacements, gradingSection);

        //    return sectionDocument;
        //}

        private byte[] PopulateDocument(byte[] documentBytes, IDictionary<string, Func<string>> replacements, TemplateGradingSection gradingSection)
        {
            using (var stream = new MemoryStream())
            {
                // Using .Write allows the memory stream to expand, the MemoryStream(byte[]) constructor does not.
                stream.Write(buffer: documentBytes,
                             offset: 0,
                             count: documentBytes.Length);

                using (var document = WordprocessingDocument.Open(stream, true))
                {
                    UpdateDataFields(document, replacements);
                    ReplaceTokens(document, replacements);
                    PopulateGradingSection(document, gradingSection);
                }

                var resultBytes = stream.ToArray();

                return resultBytes;
            }
        }

        private static void UpdateDataFields(WordprocessingDocument document, IDictionary<string, Func<string>> replacements)
        {
            foreach (var currentPart in document.MainDocumentPart.CustomXmlParts)
            {
                var xmlDocument = currentPart.GetXDocument();

                if (xmlDocument == null)
                    continue;

                var root = xmlDocument.Root;

                if (root?.Name.LocalName != TemplateConstants.TemplateDataRootName)
                    continue;

                foreach (var currentChild in root.Elements())
                {
                    var localName = currentChild.Name.LocalName;

                    if (!replacements.ContainsKey(localName))
                        continue;

                    var replacement = replacements[localName]() ?? TemplateConstants.TokenizeManualEntry(localName);

                    currentChild.SetValue(replacement);
                }

                currentPart.PutXDocument(xmlDocument);
            }
        }

        private static void ReplaceTokens(WordprocessingDocument document, IDictionary<string, Func<string>> replacements)
        {
            var mainDocument = document.MainDocumentPart;
            var headerNodes = mainDocument.HeaderParts.Select(x => x.RootElement);
            var footerNodes = mainDocument.FooterParts.Select(x => x.RootElement);
            var bodyNode = mainDocument.Document.Body;
            var allNodes = headerNodes.Concat(footerNodes).Concat(bodyNode);

            foreach (var currentNode in allNodes)
            {
                var contentRunElements = currentNode.Descendants<SdtContentRun>();
                ReplaceTokens(contentRunElements, replacements);

                var paragraphElements = currentNode.Descendants<Paragraph>();
                ReplaceTokens(paragraphElements, replacements);

                var textElements = currentNode.Descendants<Text>();
                ReplaceTokens(textElements, replacements);
            }
        }

        private static void ReplaceTokens(IEnumerable<OpenXmlElement> source, IDictionary<string, Func<string>> replacements)
        {
            var toRemove = new List<OpenXmlElement>();

            foreach (var currentElement in source)
            {
                var content = currentElement.InnerText;

                if (!replacements.Keys.Any(x => content.Contains(TemplateConstants.Tokenize(x), StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                var runs = currentElement.Elements<Run>();

                ReplaceTokens(runs, toRemove, replacements);
            }

            toRemove.ForEach(x => x.Remove());
        }

        private static void ReplaceTokens(IEnumerable<Run> source, List<OpenXmlElement> toRemove, IDictionary<string, Func<string>> replacements)
        {
            foreach (var currentRun in source)
            {
                var runContent = currentRun.InnerText;

                if (runContent.Equals(TemplateConstants.TokenPrefix) || runContent.Equals(TemplateConstants.TokenSuffix))
                {
                    toRemove.Add(currentRun);
                }
                else if (replacements.ContainsKey(runContent))
                {
                    var textElements = currentRun.Elements<Text>();
                    ReplaceTokens(textElements, replacements);
                }
                else
                {
                    if (!replacements.ContainsKey(runContent))
                        continue;

                    var textElement = currentRun.Elements<Text>().SingleOrDefault();

                    textElement.Text = replacements[runContent]() ?? TemplateConstants.TokenizeManualEntry(runContent);
                }
            }
        }

        private static void ReplaceTokens(IEnumerable<Text> source, IDictionary<string, Func<string>> replacements)
        {
            foreach (var currentElement in source)
            {
                var content = currentElement.Text;
                var doesRequireManualEntry = false;

                foreach (var currentPair in replacements)
                {
                    if (string.IsNullOrWhiteSpace(content))
                        break;

                    var key = TemplateConstants.Tokenize(currentPair.Key);

                    if (!content.Contains(key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        key = currentPair.Key;

                        if (!content.Equals(key))
                            continue;
                    }

                    var replacementValue = currentPair.Value();

                    if (replacementValue == null)
                    {
                        doesRequireManualEntry = true;
                        replacementValue = TemplateConstants.TokenizeManualEntry(currentPair.Key);
                    }

                    content = Regex.Replace(content, key, replacementValue, RegexOptions.IgnoreCase);
                }

                currentElement.Text = content;

                if (doesRequireManualEntry
                 && currentElement.Parent is Run parentRun)
                {
                    var properties = parentRun.GetFirstChild<RunProperties>();

                    if (properties == null)
                    {
                        properties = new RunProperties();
                        parentRun.Append(properties);
                    }

                    if (properties.Color == null)
                        properties.Color = new Color();

                    properties.Color.Val = "FF0000";

                    // The text must be removed and added back to the 
                    // run in order to get the run property changes
                    // to consistently apply.
                    var clonedText = currentElement.CloneNode(true);
                    currentElement.Remove();
                    parentRun.Append(clonedText);
                }
            }
        }

        private IDictionary<string, Func<string>> GetReplacements(Project project, TaskCache taskCache, Vessel vessel, TemplateSection section, TemplateGradingSection gradingSection)
        {
            var fieldReplacements = new Dictionary<string, Func<string>>(StringComparer.InvariantCultureIgnoreCase)
            {
                {
                    "VesselName",
                    () => project.VesselName
                },
                {
                    "IMONumber",
                    () => project.IMO?.ToString()
                },
                {
                    "SurveyReportNumber",
                    () => null
                },
                {
                    "DrydockCityCountry",
                    () =>
                    {
                        if (!project.DrydockLocation.HasValue)
                            return null;

                        var port = _portRepository.GetById(project.DrydockLocation.Value).Result;
                        var result = port?.CountryCode;

                        return result;
                    }
                },
                {
                    "LastVisitDate",
                    () => taskCache.GetTaskValue(project.Id, (int) TaskId.SurveyCompleted, x => x.StatusDate?.Date)?.ToString()
                },
                {
                    "FirstVisitDate",
                    () => taskCache.GetTaskValue(project.Id, (int)TaskId.SurveyStarted, x => x.StatusDate?.Date)?.ToString()
                },
                {
                    "CAPLeadName",
                    () => taskCache.GetTaskValue(project.Id, (int)TaskId.LeadSurveyorAssigned, x => x.Notes)
                },
                {
                    "CAPSecondName",
                    () => null
                },
                {
                    "CAPLeadEmployeeNumber",
                    () => null
                },
                {
                    "CAPSecondEmployer",
                    () => null
                },
                {
                    "BuilderName",
                    () => project.Builder
                },
                {
                    "VesselType",
                    () => project.ShipType.ToString()
                },
                {
                    "YearBuilt",
                    () => project.YearBuilt?.ToString()
                },
                {
                    "HullNumber",
                    () => project.HullNumber
                },
                {
                    "Length",
                    () => project.LengthOverall?.ToString()
                },
                {
                    "ClassSociety",
                    () => project.ClassSociety?.FirstOrDefault()
                },
                {
                    "ClassSocietyVesselNumber",
                    () => vessel?.ClassNum?.ToString() ?? project.AbsClassID
                },
                {
                    "Signature",
                    () => null
                },
                {
                    "FlagState",
                    () => vessel?.FlagName
                },
                {
                    "OfficialNumber",
                    () => null
                },
                {
                    "Callsign",
                    () => vessel?.CallSign
                },
                {
                    "VesselHomeport",
                    () => null
                },
                {
                    "VesselOwners",
                    () =>
                    {
                        var values = new [] { vessel?.OwnerName, vessel?.RegisteredOwner, project.CompanyName };
                        var joinedValues = values.JoinNonEmpty(", ");

                        return joinedValues;
                    }
                },
                {
                    "VesselManagers",
                    () =>
                    {
                        var values = new [] { vessel?.Manager, vessel?.Technical_Manager };
                        var joinedValues = values.JoinNonEmpty(", ");

                        return joinedValues;
                    }
                },
                {
                    "KnownPreviousVesselNames",
                    () => null
                },
                {
                    "Breadth",
                    () => vessel?.Breadth.ToString()
                },
                {
                    "Depth",
                    () => vessel?.Depth.ToString()
                },
                {
                    "SummerDraft",
                    () => vessel?.Draft.ToString()
                },
                {
                    "DeadweightTonnage",
                    () => vessel?.DWT?.ToString()
                },
                {
                    "GrossTonnage",
                    () => vessel?.GT?.ToString()
                },
                {
                    "NetTonnage",
                    () => vessel?.NetTonnage?.ToString()
                },
                {
                    "DualSociety",
                    () =>
                    {
                        var classSociety = project.ClassSociety?.Skip(1).FirstOrDefault();
                        var result = classSociety == "Other"
                                        ? project.ClassSocietyOther
                                        : classSociety;

                        return result;
                    }
                },
                {
                    "DualSocietyVesselNumber",
                    () => null
                },
                {
                    "CertificateNumber",
                    () => null
                },
                {
                    "CertificateDate",
                    () => taskCache.GetTaskValue(project.Id, (int)TaskId.SurveyCompleted, x => x.StatusDate?.Date)?.ToString()
                },
                {
                    "ClientName",
                    () => project.CompanyName
                },
                {
                    "Grade",
                    () => project.CertificateGrade
                },
                {
                    "CAPScope",
                    () => project.CapScope
                },
                {
                    "CAPScopeOther",
                    () => project.CapScopeOther
                },
                {
                    "CertificateSignature",
                    () => null
                },
                {
                    "CertificateSigneeName",
                    () => null
                },
                {
                    "CertificateSigneeTitle",
                    () => null
                },
                {
                    "SectionName",
                    () => section?.Name
                },
                {
                    "SectionParentName",
                    () => section.IsFirstChild
                            ? section?.Parent?.Name
                            : string.Empty
                },
                {
                    "AdditionalGradingNotes",
                    () => gradingSection?.AdditionalNotesDefaultValue
                }
            };

            return fieldReplacements;
        }
        private static void PopulateGradingSection(WordprocessingDocument document, TemplateGradingSection gradingSection)
        {
            if (gradingSection == null)
                return;

            var body = document.MainDocumentPart.Document.Body;

            // Remove empty headers
            var paragraphs = body.Descendants<Paragraph>();

            foreach (var currentParagraph in paragraphs)
            {
                var properties = currentParagraph.Descendants<ParagraphProperties>().FirstOrDefault();

                if (properties == null)
                    continue;

                var styleName = properties.ParagraphStyleId?.Val?.InnerText;
                var isHeader = styleName?.Contains("heading", StringComparison.OrdinalIgnoreCase) ?? false;

                if (!isHeader)
                    continue;

                if (!string.IsNullOrWhiteSpace(currentParagraph.InnerText))
                    continue;

                currentParagraph.Remove();
            }

            if (gradingSection.SurveyItems?.Any() ?? false)
                AddGradingSurveyItems(body, gradingSection);

            if (gradingSection.PhotoDescriptions?.Any() ?? false)
            {
                // Add photo description options
                var comboBoxes = body.Descendants<SdtContentComboBox>();

                foreach (var currentComboBox in comboBoxes)
                {
                    var listItems = gradingSection.PhotoDescriptions
                                                    .Select(x => new ListItem
                                                    {
                                                        DisplayText = x,
                                                        Value = x
                                                    });
                    currentComboBox.Append(listItems);
                }
            }
        }
        private static void AddGradingSurveyItems(Body body, TemplateGradingSection gradingSection)
        {
            const int gradingItemTableRowCount = 4;
            var table = body.Descendants<Table>()
                            .FirstOrDefault(x => x.Descendants<TableRow>().Count() == gradingItemTableRowCount);

            if (table == null)
                return;
            var templateRow = table.Descendants<TableRow>().Last();

            foreach (var currentItem in gradingSection.SurveyItems)
            {
                var row = templateRow.CloneNode(true);
                var cells = row.Descendants<TableCell>().ToList();

                for (var i = 0; i < cells.Count; i++)
                {
                    var currentCell = cells[i];

                    // First cell is for the item name
                    if (i == 0)
                    {
                        var currentCellText = currentCell.Descendants<Text>().FirstOrDefault();

                        if (currentCellText == null)
                            continue;

                        currentCellText.Space = SpaceProcessingModeValues.Preserve;
                        var itemName = currentItem.Name.PadLeft(currentItem.Name.Length + currentItem.Depth, '\t');
                        currentCellText.Text = itemName;
                    }
                    // When item is not gradable, we want to remove everything but the item name
                    else if (!currentItem.IsGradable)
                    {
                        var borders = currentCell.Descendants<TableCellBorders>();

                        foreach (var currentBorder in borders)
                        {
                            currentBorder.BottomBorder = null;
                            currentBorder.TopBorder = null;
                            currentBorder.LeftBorder = null;
                            currentBorder.RightBorder = null;
                            currentBorder.TopLeftToBottomRightCellBorder = null;
                            currentBorder.TopRightToBottomLeftCellBorder = null;
                        }

                        var texts = currentCell.Descendants<Text>();

                        foreach (var currentText in texts)
                            currentText.Text = string.Empty;
                    }
                }

                table.Append(row);
            }

            templateRow.Remove();
        }

    }
}
