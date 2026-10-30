using System.Collections.Generic;

namespace DigitalCap.Core.Constants
{

    public static class SQLiteQueries
    {
        public static readonly string Schema = "CAP";
        public static readonly string DbSchema = "dbo";

        public static class ProjectImageCards
        {
            public static readonly string GetDataTransferProjectImageCards = "dbo.DataTransferProjectImageCards";
            public static readonly string GetProjectImageCards = "Select * from ProjectImageCards where ProjectId=@ProjectId and IsDeleted=0 and IsSync is not null";
            public static readonly string GetProjectImageCardsByTemplateId = "Select * from ProjectImageCards where ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId and IsDeleted=0 and IsSync is not null";

            public static readonly string GetProjectImageCard = "SELECT ProjectImageCards.[Id],ProjectImageCards.[ProjectId],[TemplateId],[SectionId],[CardId],[DescriptionId],[AdditionalDescription],[CurrentCondition],ProjectImageCards.[ImageId],[CreatedDttm],ProjectImageCards.[UpdateDttm],[IsDeleted] ,Base64String as Src FROM ProjectImageCards left outer join ImageData as ImageData on ProjectImageCards.ImageId = ImageData.ImageId Where ProjectImageCards.[ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=@SectionId and IsDeleted=0";

            public static readonly string CreateProjectImageCard = "INSERT INTO ProjectImageCards ([ProjectId],[TemplateId],[SectionId],[CardId],[DescriptionId],[AdditionalDescription],[CurrentCondition],[ImageId],[CreatedDttm],[UpdateDttm],[IsDeleted],[IsSync])VALUES (IIF(@ProjectId is null or @ProjectId='',null,@ProjectId),IIF(@TemplateId is null or @TemplateId='',null,@TemplateId),IIF(@SectionId is null or @SectionId='',null,@SectionId),IIF(@CardId is null or @CardId='',null,@CardId),IIF(@DescriptionId is null or @DescriptionId=0,null,@DescriptionId),IIF(@AdditionalDescription is null or @AdditionalDescription='',null,@AdditionalDescription),IIF(@CurrentCondition is null or @CurrentCondition=0,null,@CurrentCondition),IIF(@ImageId is null or @ImageId=0,null,@ImageId),IIF(@CreatedDttm is null or @CreatedDttm='',null,@CreatedDttm),IIF(@UpdateDttm is null or @UpdateDttm='',null,@UpdateDttm),@IsDeleted,@IsSync)";

            public static readonly string UpdateProjectImageCard = "UPDATE ProjectImageCards SET [ProjectId] = @ProjectId,[TemplateId] = @TemplateId,[SectionId] =IIF( @SectionId is null,null,@SectionId),[CardId] = @CardId,[DescriptionId] = @DescriptionId,[AdditionalDescription] = @AdditionalDescription,[CurrentCondition] = @CurrentCondition,[ImageId] = @ImageId,[CreatedDttm] = @CreatedDttm,[UpdateDttm] = @UpdateDttm,[IsDeleted] = @IsDeleted,[IsSync] = CASE WHEN [IsSync] IS NOT NULL  THEN [IsSync] ELSE 0 END WHERE Id=@Id";
            public static readonly string GetImageCardById = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[CardId],[DescriptionId],[AdditionalDescription],[CurrentCondition],[ImageId],[CreatedDttm],[UpdateDttm],[IsDeleted] FROM ProjectImageCards Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=IIF(@SectionId is not null,@SectionId,[SectionId]) and [CardId]=IIF(@CardId is not null,@CardId,[CardId]) and IsDeleted=0";

            public static readonly string GetImages = "Select * from Images where Id in (Select Distinct (ImageId) from (select Distinct(ImageId) from ProjectImageCards where IsDeleted=0 and ProjectId=@ProjectId AND TemplateId=@TemplateId AND SectionId=@SectionId and IsSync is not null union select Distinct(ImageId) from GenericImageCard where  ProjectId=@ProjectId AND TemplateId=@TemplateId AND SectionId=@SectionId and IsSync is not null union select Distinct(ImageId) from TankImageCard  where ProjectId=@ProjectId AND TemplateId=@TemplateId AND SectionId=@SectionId  and IsSync is not null))";
            public static readonly string GetImageData = "Select * from ImageData where ImageId in (Select Distinct (ImageId) from (select Distinct(ImageId) from ProjectImageCards where IsDeleted=0 and ProjectId=@ProjectId AND TemplateId=@TemplateId AND SectionId=@SectionId and IsSync is not null union select Distinct(ImageId) from GenericImageCard where  ProjectId=@ProjectId AND TemplateId=@TemplateId AND SectionId=@SectionId and IsSync is not null union select Distinct(ImageId) from TankImageCard  where ProjectId=@ProjectId AND TemplateId=@TemplateId AND SectionId=@SectionId  and IsSync is not null))";
        }
        public static class GenericImageCard
        {
            public static readonly string GetGenericImageCardsByTemplateId = "Select * from GenericImageCard where ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId and IsSync is not NULL";
        }
        public static class ReleaseProject
        {
            public static readonly List<string> Release = new List<string>
        {
               "DELETE FROM Images Where ProjectId=@ProjectId",
            "DELETE FROM ImageData Where ProjectId=@ProjectId",
            "DELETE FROM ReportPart Where ProjectId=@ProjectId",
            "DELETE FROM ProjectGradingMapping Where ProjectId=@ProjectId",
            "DELETE FROM H_IReportTemplate  Where ProjectId=@ProjectId",
            "DELETE FROM Comments Where ProjectId=@ProjectId",
            "DELETE FROM ProjectGradingConditionMapping Where ProjectId=@ProjectId",
            "DELETE FROM ProjectTemplateMapping  Where ProjectId=@ProjectId",
            "DELETE FROM Activities  Where ProjectId=@ProjectId",
            "DELETE FROM ProjectImageCards  Where ProjectId=@ProjectId",
            "DELETE FROM GenericImageCard  Where ProjectId=@ProjectId",
            "DELETE FROM TankImageCard Where ProjectId=@ProjectId",
            "DELETE FROM Grading Where ProjectId=@ProjectId",
            "DELETE FROM ImageDescriptions Where ProjectId=@ProjectId",
            "DELETE FROM Vessel_GradingMapping Where ProjectId=@ProjectId",
            "DELETE FROM Vessel_TankMapping Where ImoNumber=@Imo and ProjectId=@ProjectId",
            "DELETE FROM Projects  Where ID=@ProjectId",
            "DELETE FROM ReportSurveyDetails Where ProjectId=@ProjectId",
            "DELETE FROM ReportVesselMainData Where ProjectId=@ProjectId",
            "DELETE FROM ReportStatutoryCertificates Where ProjectId=@ProjectId",
            "DELETE FROM HandIProjectGradingConditions Where ProjectId=@ProjectId",
            "DELETE FROM HandIProjectGrading Where ProjectId=@ProjectId",
            @"Delete from ProjectSectionDescriptionMapping   where SectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId)",
             @"DELETE FROM ProjectSubSectionDescriptionMapping WHERE SubSectionId IN (SELECT psm.Id  FROM ProjectSubsection psm  INNER JOIN ProjectSectionMapping ps ON ps.SectionMappingId = psm.ProjectSectionId  WHERE ps.ProjectId = @ProjectId AND ps.HasSubSection = 1);",
            @"Delete from ProjectSectionGradingMapping  where SectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId Union All Select Id  from ProjectSubSection ss where ProjectSectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId))",
             "Delete from ProjectSubSection  where ProjectSectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId)",
                "Delete from ProjectSectionMapping where ProjectId=@ProjectId",
            "Delete from ProjectTankSectionMapping   where ProjectId =@ProjectId"

        };
            public static readonly List<string> Release_BackUp = new List<string>
        {
               "DELETE FROM Images_backup",
            "DELETE FROM ImageData_backup",

            "Delete from ProjectTankSectionMapping_backup",
            "Delete from ReportPart_backup",
            "Delete from ProjectSectionDescriptionMapping_backup",
            "Delete from ProjectSubSectionDescriptionMapping_backup",
            "Delete from ProjectSectionGradingMapping_backup",
            "Delete from ProjectSectionMapping_backup",
            "Delete from ProjectSubSection_backup",
            "DELETE FROM ProjectGradingMapping_backup ",
            "DELETE FROM H_IReportTemplate_backup",
            "DELETE FROM Comments_backup",
            "DELETE FROM ProjectGradingConditionMapping_backup",
            "DELETE FROM ProjectTemplateMapping_backup",
            "DELETE FROM Activities_backup",
            "DELETE FROM ProjectImageCards_backup",
            "DELETE FROM GenericImageCard_backup",
            "DELETE FROM TankImageCard_backup",
            "DELETE FROM Grading_backup",
            "DELETE FROM ImageDescriptions_backup",
            "DELETE FROM Vessel_GradingMapping_backup",
            "DELETE FROM Vessel_TankMapping_backup",
            "DELETE FROM Projects_backup",
            "DELETE FROM ReportSurveyDetails_backup",
            "DELETE FROM ReportVesselMainData_backup",
            "DELETE FROM ReportStatutoryCertificates_backup",
            "DELETE FROM HandIProjectGrading_backup",
            "DELETE FROM HandIProjectGradingConditions_backup"

        };
            public static readonly List<string> MoveToBackUp = new List<string>
        {

               "INSERT INTO ImageData_backup SELECT * FROM ImageData WHERE ProjectId = @ProjectId;",
               "INSERT INTO Images_backup SELECT * FROM Images WHERE ProjectId = @ProjectId;",
                "INSERT INTO ReportPart_backup SELECT * FROM ReportPart WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectGradingMapping_backup SELECT * FROM ProjectGradingMapping WHERE ProjectId = @ProjectId;",
               "INSERT INTO H_IReportTemplate_backup SELECT * FROM H_IReportTemplate WHERE ProjectId = @ProjectId;",
               "INSERT INTO Comments_backup SELECT * FROM Comments WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectGradingConditionMapping_backup SELECT * FROM ProjectGradingConditionMapping WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectTemplateMapping_backup SELECT * FROM ProjectTemplateMapping WHERE ProjectId = @ProjectId;",
               "INSERT INTO Activities_backup SELECT * FROM Activities WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectImageCards_backup SELECT * FROM ProjectImageCards WHERE ProjectId = @ProjectId;",
               "INSERT INTO GenericImageCard_backup SELECT * FROM GenericImageCard WHERE ProjectId = @ProjectId;",
               "INSERT INTO TankImageCard_backup SELECT * FROM TankImageCard WHERE ProjectId = @ProjectId;",
               "INSERT INTO Grading_backup SELECT * FROM Grading WHERE ProjectId = @ProjectId;",
               "INSERT INTO ImageDescriptions_backup SELECT * FROM ImageDescriptions WHERE ProjectId = @ProjectId;",
               "INSERT INTO Vessel_GradingMapping_backup SELECT * FROM Vessel_GradingMapping WHERE ProjectId = @ProjectId;",
               "INSERT INTO Vessel_TankMapping_backup SELECT * FROM Vessel_TankMapping WHERE  ImoNumber=@Imo and ProjectId=@ProjectId;",
               "INSERT INTO Projects_backup SELECT * FROM Projects WHERE  ID = @ProjectId;",
               "INSERT INTO ReportSurveyDetails_backup SELECT * FROM ReportSurveyDetails WHERE  ProjectId = @ProjectId;",
               "INSERT INTO ReportVesselMainData_backup SELECT * FROM ReportVesselMainData WHERE ProjectId = @ProjectId;",
               "INSERT INTO ReportStatutoryCertificates_backup SELECT * FROM ReportStatutoryCertificates WHERE ProjectId = @ProjectId;",
               "INSERT INTO HandIProjectGrading_backup SELECT * FROM HandIProjectGrading WHERE ProjectId = @ProjectId;",
               "INSERT INTO HandIProjectGradingConditions_backup SELECT * FROM HandIProjectGradingConditions_backup WHERE ProjectId = @ProjectId;",
               @"insert into ProjectSectionDescriptionMapping_backup 
SELECT * from ProjectSectionDescriptionMapping   where SectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId)",
               @"insert into ProjectSubSectionDescriptionMapping_backup 
SELECT * from ProjectSubSectionDescriptionMapping   where SubSectionId in (Select Id  from ProjectSubSection pss
inner join ProjectSectionMapping psm on psm.SectionMappingId = pss.ProjectSectionId
where psm.ProjectId=@ProjectId
)",
               @"Insert Into ProjectSectionGradingMapping_backup 
Select * from ProjectSectionGradingMapping  where SectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId
Union All
Select Id  from ProjectSubSection ss where ProjectSectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId))",
               @"Insert Into ProjectSubSection_backup 
Select * from ProjectSubSection  where ProjectSectionId in (Select SectionMappingId  from ProjectSectionMapping psm where ProjectId=@ProjectId)",
               @"insert Into ProjectSectionMapping_backup 
Select * from ProjectSectionMapping where ProjectId=@ProjectId",
               @"Insert Into ProjectTankSectionMapping_backup
Select * from ProjectTankSectionMapping   where ProjectId =@ProjectId"


        };
            public static readonly List<string> BackToTable = new List<string>
        {

               "INSERT INTO ImageData SELECT * FROM ImageData_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO Images SELECT * FROM Images_backup WHERE ProjectId = @ProjectId;",
                "INSERT INTO ReportPart SELECT * FROM ReportPart_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectGradingMapping SELECT * FROM ProjectGradingMapping_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO H_IReportTemplate SELECT * FROM H_IReportTemplate_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO Comments SELECT * FROM Comments_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectGradingConditionMapping SELECT * FROM ProjectGradingConditionMapping_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectTemplateMapping SELECT * FROM ProjectTemplateMapping_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO Activities SELECT * FROM Activities_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO ProjectImageCards SELECT * FROM ProjectImageCards_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO GenericImageCard SELECT * FROM GenericImageCard_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO TankImageCard SELECT * FROM TankImageCard_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO Grading SELECT * FROM Grading_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO ImageDescriptions SELECT * FROM ImageDescriptions_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO Vessel_GradingMapping SELECT * FROM Vessel_GradingMapping_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO Vessel_TankMapping SELECT * FROM Vessel_TankMapping_backup WHERE  ImoNumber=@Imo and ProjectId = @ProjectId;",
               "INSERT INTO Projects SELECT * FROM Projects_backup WHERE  ID = @ProjectId;",
               "INSERT INTO ReportSurveyDetails SELECT * FROM ReportSurveyDetails_backup WHERE  ProjectId = @ProjectId;",
               "INSERT INTO ReportVesselMainData SELECT * FROM ReportVesselMainData_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO ReportStatutoryCertificates SELECT * FROM ReportStatutoryCertificates_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO HandIProjectGrading SELECT * FROM HandIProjectGrading_backup WHERE ProjectId = @ProjectId;",
               "INSERT INTO HandIProjectGradingConditions SELECT * FROM HandIProjectGradingConditions_backup WHERE ProjectId = @ProjectId;",
               @"insert into ProjectSectionDescriptionMapping 
SELECT * from ProjectSectionDescriptionMapping_backup ",
               @"insert into ProjectSubSectionDescriptionMapping 
SELECT * from ProjectSubSectionDescriptionMapping_backup ",
               @"Insert Into ProjectSectionGradingMapping 
Select * from ProjectSectionGradingMapping_backup",
               @"Insert Into ProjectSubSection
Select * from ProjectSubSection_backup",
               @"insert Into ProjectSectionMapping 
Select * from ProjectSectionMapping_backup",
               @"Insert Into ProjectTankSectionMapping
Select * from ProjectTankSectionMapping_backup"

        };

            public static readonly List<string> KeepDownloaded = new List<string>
            {
                "Update ProjectGradingMapping  set IsSync=null   Where ProjectId=@ProjectId and IsSync is not null",
                "Update H_IReportTemplate set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update ProjectGradingConditionMapping set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update ProjectTemplateMapping set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update  ProjectImageCards set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update  GenericImageCard set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update TankImageCard set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update Vessel_TankMapping set IsSync=null Where ImoNumber=@Imo and ProjectId=@ProjectId and IsSync is not null",
                "Update Images set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update ImageData set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update HandIProjectGrading set IsSync=null Where ProjectId=@ProjectId and IsSync is not null",
                "Update HandIProjectGradingConditions set IsSync=null Where ProjectId=@ProjectId and IsSync is not null"

            };
        }
        public static class ProjectReport
        {
            public static readonly string GetProjectExportConfigForSections = @"
WITH ImoValue AS (
    SELECT Imo FROM projects WHERE Id = @ProjectId
)
SELECT 
    SectionId, 
    SectionName, 
    RequiredInReport, 
    TankType,
    TankTypeId, 
    [Order], 
    CAST(ParentId AS UNIQUEIDENTIFIER) AS ParentId, 
    IsSubSection,
    CreatedDttm
FROM 
(
    -- Main Sections
    SELECT 
        SectionMappingId AS SectionId, 
        SectionName, 
        RequiredInReport, 
        null as TankType,
        0 AS TankTypeId,        
        [Order], 
        NULL AS ParentId, 
        0 AS IsSubSection,
        NULL as CreatedDttm
    FROM ProjectSectionMapping  
    WHERE 
        ProjectId = @ProjectId 
        AND PartId = @PartId 
        AND IsActive = 1 
        AND IsDeleted = 0   
    
    UNION ALL

    -- Subsections
    SELECT 
        sub.Id AS SectionId, 
        sub.SectionName, 
        section.RequiredInReport,
        null as TankType,
        0 AS TankTypeId, 
        section.[Order], 
        section.SectionMappingId AS ParentId, 
        1 AS IsSubSection,
        NULL as CreatedDttm
    FROM ProjectSectionMapping section   
    INNER JOIN ProjectSubSection sub 
        ON sub.ProjectSectionId = section.SectionMappingId 
        AND section.HasSubSection = 1    
    WHERE 
        section.ProjectId = @ProjectId 
        AND section.IsActive = 1 
        AND section.IsDeleted = 0
        AND PartId= @PartId

 UNION all
  
    
    SELECT DISTINCT
    Vessel_TankMapping.Id AS SectionId, 
    Vessel_TankMapping.TankName AS SectionName, 
    Vessel_TankMapping.RequiredInReport, 
    tt.TankType,
    tt.Id AS TankTypeId,
    999 + ROW_NUMBER() OVER (
        ORDER BY 
            tt.Id,
            Vessel_TankMapping.TankName,
            Vessel_TankMapping.CreatedDttm
           
    ) AS [Order], 
    NULL AS ParentId, 
    0 AS IsSubSection,
    Vessel_TankMapping.CreatedDttm AS CreatedDttm
FROM Vessel_TankMapping  
INNER JOIN ProjectTankSectionMapping psm
    ON Vessel_TankMapping.TankTypeId = psm.TankTypeId
INNER JOIN TankType tt 
    ON tt.Id = Vessel_TankMapping.TankTypeId
WHERE 
    psm.PartId = @PartId
    AND psm.ProjectId = @ProjectId
    AND psm.IsActive = 1
    AND psm.IsDeleted = 0
    AND Vessel_TankMapping.ProjectId = @ProjectId
    AND Vessel_TankMapping.IsActive = 1 
    AND Vessel_TankMapping.IsDeleted = 0
ORDER BY 
    tt.Id,
    Vessel_TankMapping.CreatedDttm,
    Vessel_TankMapping.TankName

) AS result   
ORDER BY [Order],TankTypeId,CreatedDttm";

            //public static readonly string GetProjectExportConfigForSections = @"WITH ImoValue AS ( SELECT Imo FROM cap_Projects WHERE Id = @Projectid)    +
            //    SELECT SectionId, SectionName, RequiredInReport, TankTypeId, [Order], ParentId, IsSubSection  FROM ( SELECT SectionMappingId AS SectionId, SectionName, RequiredInReport, 0 AS TankTypeId, [Order], NULL AS ParentId, 0 AS IsSubSection FROM config_ProjectSectionMapping  WHERE ProjectId = @ProjecTId AND PartId = @PartId AND IsActive = 1 AND IsDeleted = 0   
            //    UNION ALL
            //   SELECT sub.Id AS SectionId, sub.SectionName, section.RequiredInReport, 0 AS TankTypeId, section.[Order], section.SectionMappingId AS ParentId, 1 AS IsSubSection FROM config_ProjectSectionMapping section   INNER JOIN config_ProjectSubSection sub ON sub.ProjectSectionId = section.SectionMappingId AND section.HasSubSection = 1    WHERE section.ProjectId = ? AND section.IsActive = 1 AND section.IsDeleted = 0
            //    UNION ALL 
            //    SELECT Id AS SectionId, tankName AS SectionName, RequiredInReport, TankTypeId, 999 AS [Order], NULL AS ParentId, 0 AS IsSubSection     FROM Vessel_TankMapping    WHERE IsActive = 1 AND IsDeleted = 0    AND ImoNumber = (SELECT Imo FROM ImoValue)    AND TankTypeId IN (   SELECT TankTypeId FROM config_ProjectTankSectionMapping  WHERE ProjectId = ? AND IsActive = 1 AND IsDeleted = 0 AND PartId =@PartId   )) AS result   ORDER BY [Order]";
            public static readonly string GetProjectExportConfigForParts = $"select PartId as PartId, PartName, RequiredInReport from ReportPart	where ProjectId = @ProjectId";
            public static readonly string GetReportPartsByProjectId = $"SELECT PartId,PartName,SequenceNo FROM ReportPart WHERE ProjectId = @projectId UNION ALL SELECT 1 AS PartId,'Vessel Main Data' AS PartName,0 AS SequenceNo UNION ALL SELECT 2 AS PartId,'Part H&I' AS PartName,20 AS SequenceNo ORDER BY SequenceNo";
            public static readonly string GetTemplateTitle = @"SELECT * 
    FROM (
        SELECT 
            PartId, 
            PartName, 
            SequenceNo, 
            ProjectId
        FROM ReportPart
        
        UNION ALL

        SELECT 
            1 AS PartId,    
            'Vessel Main Data' AS PartName,
            0 AS SequenceNo,
            @ProjectId AS ProjectId

        UNION ALL

        SELECT 
            2 AS PartId,
            'Part H&I' AS PartName,
            20 AS SequenceNo,
            @ProjectId AS ProjectId
    ) AS result
    WHERE PartId = @PartId AND ProjectId = @ProjectId;";
            public static readonly string GetNormalsectionsByProjectId = $"select SectionMappingId as SectionMappingId,PartId,SectionName,SubHeader,TotalCards,PlaceholderCount,FileNameCount,[Order],HasSubSection from ProjectSectionMapping where PartId = @PartId and  ProjectId= @ProjectId order by [Order]";
            public static readonly string GetProjectSubSection = $"SELECT P_SS.Id AS SectionMappingId,P_SM.SectionMappingId as Id,P_SS.SectionName  as SectionName,P_SS.SectionName AS SpecialSectionName,P_SS.Totalcards,P_SS.FileNameCount,P_SS.PlaceholderCount,P_SS.[OrderNumber] as [Order] FROM ProjectSubSection P_SS left join ProjectSectionMapping as P_SM on P_SM.SectionMappingId = P_SS.ProjectSectionId WHERE P_SS.ProjectSectionId = @SectionId and P_Sm.IsActive =1 and P_SM.IsDeleted=0";
            public static readonly string GetTankSectionsByProjectid = $"SELECT distinct Vessel_TankMapping.Id,TankSectionMapping.TankSectionMappingId as SectionMappingId,TankSectionMapping.PartId,Vessel_TankMapping.TankTypeId,Vessel_TankMapping.TankName,TankSectionMapping.TotalCards ,TankSectionMapping.PlaceholderCount,TankSectionMapping.FileNameCount,Vessel_TankMapping.Subheader FROM Vessel_TankMapping INNER JOIN ProjectTankSectionMapping as TankSectionMapping ON Vessel_TankMapping.TankTypeId = TankSectionMapping.TankTypeId WHERE TankSectionMapping.PartId = @PartId AND TankSectionMapping.ProjectId = @ProjectId AND Vessel_TankMapping.ImoNumber = @ImoNumber AND Vessel_TankMapping.ProjectId = @ProjectId AND Vessel_TankMapping.IsDeleted=0 AND Vessel_TankMapping.IsActive=1 AND TankSectionMapping.IsActive =1 AND TankSectionMapping.IsDeleted =0 order by  Vessel_TankMapping.TankName,Vessel_TankMapping.TankTypeId,Vessel_TankMapping.CreatedDttm";
            public static readonly string GetTanksByTypeId = $"select Vessel_TankMapping.Id,TankName,TankTypeId from Vessel_TankMapping inner join TankType on Vessel_TankMapping.TankTypeId=TankType.Id Where  IsActive=1 and  ImoNumber =@ImoNumber and TankTypeId=@TankTypeId Order By TankTypeId,TankName";
            public static readonly string GetImageDescriptionsByTankTypeId = $"select * from (select descr.Id,descr.Description from ImageDescriptions descr  inner join Tank_ImageDescriptionMapping tank on descr.CategoryId =tank.CategoryId inner join TankType tt on tank.TankTypeId = tt.Id where ProjectId=@ProjectId and tank.TankTypeId=@TankTypeId and IsActive=1)TankGrading order by Id";
            public static readonly string GetGradingConditionByProjectId = $"select  Id,SectionId,GradingId,[GradingConditionLabel] as [Name], GradingConditionValue as [Value] from ProjectGradingConditionMapping where ProjectId=@ProjectId and IsDeleted=0";
            public static readonly string GetGradigCondition = $"select GradingCondition.Id,Condition as Name from GradingCondition";
            public static readonly string GetProjectSectionGrading = $"select grading.Id, grading.LabelName as [Name],grading.GradingConditionGroupId ,grading.GradingGroupId,P_SG.SectionId,grading.RequiredInReport from ProjectSectionGradingMapping P_SG inner join Grading grading on grading.GradingGroupId = P_SG.CategoryId where P_SG.SectionId = @SectionId  and P_SG.IsActive=1 and P_SG.IsDeleted= 0 and grading.IsActive=1 and grading.IsDeleted= 0 and grading.ProjectId = @ProjectId order by grading.LabelName";
            public static readonly string GetH_IGrading = $"SELECT distinct Id, LabelName as [Name] FROM H_I_Grading WHERE SpecialGasGrading = @GasGrading";
            public static readonly string GetProjectSectionDescription = $"select descriptions.Id,descriptions.Description  from ProjectSectionDescriptionMapping P_SD inner join ImageDescriptions descriptions on descriptions.CategoryId = P_SD.CategoryId and descriptions.ProjectId = @ProjectId where P_SD.SectionId = @SectionId  and P_SD.IsActive=1 and P_SD.IsDeleted= 0 and descriptions.IsActive=1 and descriptions.IsDeleted= 0";
            public static readonly string GetProjectSubSectionDescription = $"select descriptions.Id,descriptions.Description  from ProjectSubSectionDescriptionMapping P_SD inner join ImageDescriptions descriptions on descriptions.CategoryId = P_SD.CategoryId and descriptions.ProjectId = @ProjectId where P_SD.SubSectionId = @SectionId  and P_SD.IsActive=1 and P_SD.IsDeleted= 0 and descriptions.IsActive=1 and descriptions.IsDeleted= 0";

            public static readonly string GetGenericImageCard = $"select GenericImageCard.Id,CardName,CardNumber,DescriptionId,AdditionalDescription,CurrentCondition,GenericImageCard.ImageId,IsActive,Base64String as src from GenericImageCard  left outer join  ImageData as ImageData on GenericImageCard.ImageId = ImageData.ImageId Where GenericImageCard.ProjectId=@ProjectId and GenericImageCard.TemplateId=@TemplateId and GenericImageCard.SectionId=@SectionId and GenericImageCard.IsActive=1 and ( IsDeleted=0 or IsDeleted is null)";
            public static readonly string CreateGenericImageCard = $"INSERT INTO GenericImageCard([ProjectId],[TemplateId],[SectionId],[CardNumber],[DescriptionId],[AdditionalDescription],[CurrentCondition],[ImageId],[CreatedDttm],[UpdatedDttm],[IsActive],[CardName],[IsDeleted],[IsSync]) VALUES(@ProjectId,@TemplateId,@SectionId,@CardNumber,@DescriptionId,@AdditionalDescription,@CurrentCondition,@ImageId,@CreatedDttm,@UpdatedDttm,@IsActive,@CardName,0,1)";
            public static readonly string UpdateGenericImageCard = $"UPDATE GenericImageCard SET [ProjectId] = @ProjectId,[TemplateId] = @TemplateId,[SectionId] =@SectionId,[CardNumber] = @CardNumber,[DescriptionId] = @DescriptionId,[AdditionalDescription] = @AdditionalDescription,[CurrentCondition] = @CurrentCondition,[ImageId] = @ImageId,[CreatedDttm] = @CreatedDttm,[UpdatedDttm] = @UpdatedDttm,[IsDeleted] = @IsDeleted,[IsActive] = @IsActive,[CardName] = @CardName,[IsSync]=Case When [IsSync] is not NULL then [IsSync] else 0 end  WHERE Id=@Id";
            public static readonly string GetGenericImageCardById = $"SELECT [Id],[ProjectId],[TemplateId],[SectionId],[CardNumber],[DescriptionId], [AdditionalDescription],[CurrentCondition],[ImageId],[CreatedDttm],[UpdatedDttm],[IsActive],[CardName],[IsDeleted] FROM GenericImagecard Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=@SectionId and [CardNumber]=@CardNumber and IsActive=1 and (IsDeleted=0 or IsDeleted is NULL)";

            public static readonly string GetHandIProjectGradingByProjectId = $"select Id,ProjectId,GradingId,[Value] from HandIProjectGrading where ProjectId=@ProjectId and IsActive = 1 and IsDeleted=0";
            public static readonly string GetHandIProjectGradingById = $"select Id,ProjectId,GradingId,[Value] from [HandIProjectGrading] where ProjectId=@ProjectId and GradingId= @GradingId and IsActive = 1 and IsDeleted=0";
            public static readonly string CreateHandIProjectGrading = $"INSERT INTO HandIProjectGrading (GradingID, ProjectId,  [Value], IsActive, IsDeleted, CreatedBY, CreatedOn,IsSync) VALUES (@GradingID, @ProjectId, @Value, @IsActive, 0, 'auto', @CreatedDttm,1)";
            public static readonly string UpdateHandIProjectGrading = $"update HandIProjectGrading set [Value] = @Value,ModifiedON= @UpdatedDttm,IsSync = Case When IsSync is not null then IsSync else 0 end  where ProjectId=@ProjectId and GradingId= @GradingId and IsActive = 1 and IsDeleted=0";

            public static readonly string GetHandIProjectGradingConditionByProjectId = $"select Id,ProjectId,GradingId,ConditionLabel as GradingConditionLabel,[Value] as GradingConditionValue  from HandIProjectGradingConditions where ProjectId=@ProjectId and IsActive = 1 and IsDeleted=0";
            public static readonly string GetHandIProjectGradingConditionById = $"select Id,ProjectId,GradingId,ConditionLabel as GradingConditionLabel,[Value] as GradingConditionValue from HandIProjectGradingConditions where ProjectId=@ProjectId and GradingId=@GradingId and IsActive = 1 and IsDeleted=0 and ConditionLabel=@ConditionLabel";
            public static readonly string UpdateHandIProjectGradingCondition = $"update HandIProjectGradingConditions set [Value] = @Value,ModifiedON= @ModifiedOn,IsSync=Case When IsSync is not null then IsSync else 0 end where ProjectId=@ProjectId and GradingId= @GradingId and IsActive = 1 and IsDeleted=0 and   ConditionLabel=@ConditionLabel";
            public static readonly string CreateHandIProjectGradingCondition = $"INSERT INTO HandIProjectGradingConditions (GradingID,ProjectId,ConditionLabel,[Value],IsActive,IsDeleted,CreatedBY,CreatedOn,IsSync) VALUES (@GradingID,@ProjectId,@ConditionLabel,@Value,@IsActive,@IsDeleted,'auto',@CreatedDttm,1)";
            public static readonly string CheckToShowSectionExportConfig = $"SELECT     (SELECT COUNT(*)      FROM ProjectSectionMapping      WHERE ProjectId = @ProjectId AND RequiredInReport = 0 AND PartId=@PartId)     +     (SELECT COUNT(*)      FROM Vessel_TankMapping vt     INNER JOIN ProjectTankSectionMapping ptsm          ON ptsm.TankTypeId = vt.TankTypeId      WHERE vt.ImoNumber = @Imonumber        AND vt.RequiredInReport = 0  AND vt.ProjectId = @ProjectId      AND ptsm.PartId = @PartId)     AS Result";
            public static readonly string CheckToShowAllSectionExportConfig = $"SELECT     (SELECT COUNT(*)      FROM ProjectSectionMapping      WHERE ProjectId = @ProjectId AND RequiredInReport = 0)     +     (SELECT COUNT(*)      FROM Vessel_TankMapping vt     INNER JOIN ProjectTankSectionMapping ptsm          ON ptsm.TankTypeId = vt.TankTypeId      WHERE vt.ImoNumber = @Imonumber       AND vt.RequiredInReport = 0   AND vt.ProjectId = @ProjectId     )     AS Result";
        }


        public static class Project
        {
            public static readonly string Create = $"{Schema}.Create_Project";
            public static readonly string Update = $"{Schema}.Update_Project";
            public static readonly string Cancel = $"{Schema}.Cancel_Project";
            public static readonly string Delete = $"{Schema}.Delete_Project";
            public static readonly string Read = $"{Schema}.Read_Project";


            public static readonly string ReadById = "SELECT p.*, u.ProviderDisplayName AS CreatedByDisplayName, u2.ProviderDisplayName AS StatusChangedByDisplayName FROM Projects p LEFT JOIN AspNetUserLogins u on p.CreatedBy = u.UserId LEFT JOIN AspNetUserLogins u2 on p.StatusChangedBy = u2.UserId WHERE ID = @ID";

            //public static readonly string ReadById = $"{Schema}.Read_Project_ById";

            public static readonly string SummaryById = $"{Schema}.Read_Project_Summary_ById";
            public static readonly string DetailsById = $"{Schema}.Read_Project_Details";



            //public static readonly string ByStatus = $"select s.Status as Category, count(p.ID) as Value, case when s.Status = 'No Data Submitted' then 'rgb(0,47,108)' when s.Status = 'Contracted' then 'rgb(219, 220, 198)' when s.Status = 'Open' then 'rgb(113, 194, 255)' when s.Status = 'Active' then 'rgb(94, 140, 202)' when s.Status = 'Ready for Closure' then 'rgb(80, 158, 47)' when s.Status = 'Closed' then 'rgb(0, 42, 78)' else 'rgb(99,102,106)'  end as Color from Projects p inner join Statuses s on p.ProjectStatus = s.ID group by s.status";

            public static readonly string ByStatus = $"SELECT json_group_array(json_object('Category', Category,'Value', Value,'Color', Color)) AS JSONResult FROM( SELECT s.Status AS Category, COUNT(p.ID) AS Value,CASE WHEN s.Status = 'No Data Submitted' THEN 'rgb(0,47,108)' WHEN s.Status = 'Contracted' THEN 'rgb(219, 220, 198)' WHEN s.Status = 'Open' THEN 'rgb(113, 194, 255)' WHEN s.Status = 'Active' THEN 'rgb(94, 140, 202)' WHEN s.Status = 'Ready for Closure' THEN 'rgb(80, 158, 47)' WHEN s.Status = 'Closed' THEN 'rgb(0, 42, 78)' ELSE 'rgb(99,102,106)' END AS Color FROM Projects p INNER JOIN Statuses s ON p.ProjectStatus = s.ID GROUP BY s.Status) AS Result;";









            //public static readonly string ByStatus = $"{Schema}.Read_Projects_ByStatus";
            public static readonly string ReadStatuses = $"{Schema}.Read_Project_Statuses";
            public static readonly string UpdateProjectStatus = $"{Schema}.Update_Project_Status_Manual";
            public static readonly string UpdatePriority = "UPDATE Projects SET ProjectPriority = @Priority, LastModifiedBy = @User, ModifiedDate = DATETIME('now') WHERE ID = @Id";
            public static readonly string UpdatePercentComplete = "UPDATE Projects SET PercentComplete = @PercentComplete, LastModifiedBy = @User, ModifiedDate = DATETIME('now') WHERE ID = @ID";
            public static readonly string Grading = $"{Schema}.Read_Project_Grading";


            public static readonly string Count = $"SELECT json_group_array(json_object('Count', cnt)) AS JSONResult FROM(SELECT COUNT(ID) AS cnt FROM Projects);";




            //public static readonly string Count = $"{Schema}.Read_Projects_Count";


            public static readonly string AllProjectsGrid = $"SELECT p.ID,'Offline' as IsDownload, p.Name, p.PIDNumber, p.ProjectComments, p.CompanyName, p.IMO, p.WCN, p.PotentialDrydockDate, p.CapRegion, p.ModifiedDate, p.LastModifiedBy, p.ProjectPriority, s.[Status] AS ProjectStatusName,  c.CreatedByDisplayName AS CommentCreatedBy, c.Comment AS MostRecentComment, c.CommentLastModifiedDate, a.ProjectLastUpdatedDate, p.PercentComplete FROM Projects p INNER JOIN Statuses s ON s.ID = p.ProjectStatus LEFT JOIN (SELECT ProjectId, Comment, u.ProviderDisplayName as CreatedByDisplayName, LastModifiedDate AS CommentLastModifiedDate, ROW_NUMBER() OVER (PARTITION BY ProjectId ORDER BY LastModifiedDate DESC) AS rn FROM Comments c LEFT JOIN AspNetUserLogins u on c.CreatedBy = u.UserId) c ON p.ID = c.ProjectId AND c.rn = 1 LEFT JOIN ( SELECT ProjectId, MAX(CreateDttm) AS ProjectLastUpdatedDate FROM Activities GROUP BY ProjectId) a ON p.ID = a.ProjectId";





            //public static readonly string AllProjectsGrid = $"{Schema}.Read_Projects_Grid";
            public static readonly string AllProjectsGridExport = $"{Schema}.Read_Projects_Grid_Export";
            public static readonly string ReadByClientId = $"{Schema}.Read_Projects_ByClientId";
            public static readonly string ProjectCountGroupedByClientId = $"{Schema}.Read_Project_Count_Grouped_ByClientId";


            public static readonly string ReadProjectNameById = $"SELECT Name FROM Projects WHERE ID = @ID";

            //public static readonly string ReadProjectNameById = $"{Schema}.Read_Project_Name_ById";

            public static readonly string ReadIMONumberByProjectId = $"SELECT IMO FROM Projects WHERE ID = @ProjectId";
            public static readonly string UpdateReleasedProject = $"update Projects set IsReadOnlyEnabled=true where Id=@ProjectId";

            //public static readonly string ReadIMONumberByProjectId = $"{Schema}.Read_IMONumber_ByProjectId";
            public static readonly string ReadProjectByImoNumber = $"SELECT p.*, u.ProviderDisplayName AS CreatedByDisplayName, u2.ProviderDisplayName AS StatusChangedByDisplayName FROM Projects p LEFT JOIN AspNetUserLogins u on p.CreatedBy = u.UserId LEFT JOIN AspNetUserLogins u2 on p.StatusChangedBy = u2.UserId WHERE p.IMO  = @IMO";
            public static readonly string CheckProjectExistsByImoNumber = $"select DISTINCT IMO from projects where IMO=@IMO";
            public static readonly string ReadProjectVesselTypeById = $"select VesselType from projects Where Id = @ID";
            public static readonly string ReadProjectExistsByProjectName = $"SELECT DISTINCT Name FROM [Projects] WHERE Name = @ProjectName and IMO = @IMO and ShipType = @VesselTypeID";
            public static readonly string GetProjectNamesByIMO = $"SELECT DISTINCT IMO as Value,Name AS Text,ID as ProjectId FROM [Projects] where IMO is not null and IMO=@IMO order by IMO";
            public static readonly string GetProjectListByIMO = $"SELECT DISTINCT Id as value,Name AS text FROM [Projects] where IMO is not null and IMO=@IMO order by Id";

        }

        public static class Projects
        {
            public static readonly string UpdatePercentComplete = Project.UpdatePercentComplete;
            public static readonly string UpdatePriority = Project.UpdatePriority;
        }

        public static class ProjectGrading
        {
            public static readonly string GetGradingById = ProjectMapping.GetGradingbyId;
        }

        public static class File
        {
            public static readonly string ById = $"{Schema}.Read_File_ById";

            public static readonly string ByProjectId = "SELECT * FROM Files WHERE ProjectId = @ProjectId AND COALESCE(SurveyImage, 0) != 1";

            //public static readonly string ByProjectId = $"{Schema}.Read_Files_ByProjectId";

            public static readonly string ByTaskId = $"{Schema}.Read_Files_ByTaskId";
            public static readonly string DeleteById = $"{Schema}.Delete_File";
            public static readonly string Create = $"{Schema}.Create_File";
            public static readonly string Update = $"{Schema}.Update_File";
        }

        public static class Task
        {
            public static readonly string CreateTask = $"{Schema}.Create_Task";
            public static readonly string ReadTasks = $"{Schema}.Read_Tasks";
            public static readonly string ReadById = $"{Schema}.Read_Tasks_ByTaskId";


            public static readonly string ReadCap2ById = $"select pts.[ID],pts.[ProjectId],t.[Category],t.[Cap2Type] as [Type],t.[Cap2Name] as [Task],t.IsMilestone,t.PercentageComplete,s.[Status],pts.StatusId,pts.[Value],pts.[Notes],pts.StatusDate,pts.[LastModifiedDate],pts.LastModifiedBy,pts.DueDate,pts.AssignedTo,pts.AssignedDate,pts.SurveyDays from ProjectTaskStatus pts inner join Statuses s on pts.[StatusId] = s.[ID] inner join Tasks t on pts.[TaskId] = t.[ID] where pts.[ID] = @ID";

            //public static readonly string ReadCap2ById = $"{Schema}.Read_Cap2Tasks_ByTaskId";

            public static readonly string ReadTasksByProjectId = $"{Schema}.Read_TasksByProjectId";
            public static readonly string ReadByProjectId = $"{Schema}.Read_Tasks_ByProjectId";


            public static readonly string ReadCap2TasksByProjectId = "SELECT pts.[ID],pts.[ProjectId],pts.[StatusId],t.[Id] AS TaskId,t.[Category],t.[Cap2Type] AS [Type],t.[Cap2Name] AS Task,t.IsMilestone,t.PercentageComplete,s.[Status],pts.[Value],pts.[Notes],pts.StatusDate,pts.LastModifiedDate,pts.LastModifiedBy,pts.DueDate,pts.AssignedTo,pts.AssignedDate,CAST(CASE WHEN pts.StatusId IN (3, 4) THEN 0 WHEN pts.StatusId = 1 THEN CASE WHEN pts.LastModifiedDate IS NULL THEN julianday('now') - julianday(pts.StatusDate) ELSE julianday(pts.LastModifiedDate) - julianday(pts.StatusDate) END ELSE julianday('now') - julianday(pts.StatusDate) END AS INTEGER) AS DaysWorked,(SELECT COUNT(*) FROM Comments c WHERE c.ProjectTaskId = pts.ID) AS NumberOfComments,(SELECT COUNT(*) FROM Files f WHERE f.TaskId = pts.ID) AS NumberOfAttachments FROM ProjectTaskStatus pts INNER JOIN Statuses s ON pts.StatusId = s.[ID] INNER JOIN Tasks t ON pts.[TaskId] = t.[ID] WHERE pts.[ProjectId] = @ID";

            //public static readonly string ReadCap2TasksByProjectId = $"{Schema}.Read_Cap2Tasks_ByProjectId";


            public static readonly string ReadTasksByType = $"{Schema}.Read_TasksByType";
            public static readonly string ReadTasksByTypeAndProjectId = $"{Schema}.Read_Tasks_ByTypeAndProjectId";
            public static readonly string ReadCap2TasksByTypeAndProjectId = $"{Schema}.Read_Cap2Tasks_ByTypeAndProjectId";
            public static readonly string ReadIncompleteTasksByCategoryAndProjectId = $"{Schema}.Read_Incomplete_Tasks_ByCategoryAndProjectId";
            public static readonly string UpdateTask = $"{Schema}.Update_Task";
            public static readonly string UpdateByTypeIdAndProjectId = $"{Schema}.Update_Tasks_ByTypeIdAndProjectId";
            public static readonly string UpdateTechnicalTasksComplete = $"{Schema}.Update_TechnicalTasksComplete";
            public static readonly string UpdateInvoiceTasksComplete = $"{Schema}.Update_InvoiceTasksComplete";
            public static readonly string ReadStatuses = $"{Schema}.Read_Task_Statuses";
            public static readonly string ReadIncompleteByCategoryAndProjectId = $"{Schema}.Read_Incomplete_Tasks_ByCategoryAndProjectId";
            public static readonly string ReadCap2TasksForProjects = $"{Schema}.Read_Cap2Tasks_ForProjects";
        }

        public static class Vessel
        {
            public static readonly string DetailsByIMONumber = $"{Schema}.Read_VesselDetails_ByIMONumber";
            public static readonly string LookUpByIMONumber = $"{Schema}.Read_VesselLookUp_ByIMONumber";
            //CRUD Procedure for Vesssel main data

            public static readonly string Create_VesselMainData = $"INSERT INTO ReportVesselMainData([projectId],[reportNo],[portDate],[actualReportStartDate],[vesselName],[vesselID],[imoNumber],[vesselType],[yearBuilt],[builtBy],[hullNo],[flagName],[homeport],[officalNumber],[callSign],[previousName],[ownerName],[manager],[length],[breadth],[depth],[draft],[deadweight],[grossTons],[netTons],[propulsionType],[kw],[shaftRPM],[propulsionManufacturer],[gearManufacturer],[classed],[classNo],[classNotation],[machinery],[other],[environment],[firstVisitDate],[lastVisitDate],SelectedPrefix) VALUES(@projectId,@reportNo,@portDate,@actualReportStartDate,@vesselName,@vesselID,@imoNumber,@vesselType,@yearBuilt,@builtBy,@hullNo,@flagName,@homeport,@officalNumber,@callSign,@previousName,@ownerName,@manage,@length,@breadth,@depth,@draft,@deadweight,@grossTons,@netTons,@propulsionType,@kw,@shaftRPM,@propulsionManufacturer,@gearManufacturer,@classed,@classNo,@classNotation,@machinery,@other,@environment,@firstVisitDate,@lastVisitDate,@selectedPrefix)";

            //public static readonly string Create_VesselMainData = $"{DbSchema}.Insert_VesselDetails";

            public static readonly string Update_VesselMainData = $"UPDATE ReportVesselMainData SET [projectId]=@projectId,[reportNo]=IFNull(@reportNo,[reportNo]),[portDate]=IFNull(@portDate,[portDate]),[actualReportStartDate]=IFNull(@actualReportStartDate,[actualReportStartDate]),[vesselName]=IFNull(@vesselName,[vesselName]),[vesselID]=IFNull(@vesselID,[vesselID]),[imoNumber]=IIF(@imoNumber!=0,@imoNumber,[imoNumber]),[vesselType]=IFNull(@vesselType,[vesselType]),[yearBuilt]=IIF(CAST(@yearBuilt AS INT)!=0,@yearBuilt,[yearBuilt]),[builtBy]=IFNull(@builtBy,[builtBy]),[hullNo]=IFNull(@hullNo,[hullNo]),[flagName]=IFNull(@flagName,[flagName]),[homeport]=IFNull(@homeport,[homeport]),[officalNumber]=IFNull(@officalNumber ,[officalNumber]),[callSign]=IFNull(@callSign,[callSign]),[previousName]=IFNull(@previousName ,[previousName]),[ownerName]=IFNull(@ownerName,[ownerName]),[manager]=IFNull(@manager,[manager]),[length]=IIF(cAST(@length  aS decimal)!=0,@length,[length]),[breadth]=IIF(cast(@breadth as decimal)!=0,@breadth ,[breadth]),[depth]=IIF(cast(@depth as decimal)!=0,@depth,[depth]),[draft]=IIF(cast(@draft as decimal)!=0,@draft,[draft]),[reportPort]=IFNull(@reportPort,[reportPort]),[deadweight]=IIF(cast(@deadweight  as decimal)!=0,@deadweight,[deadweight]),[grossTons]=IIF(cast(@grossTons as decimal)!=0,@grossTons ,[grossTons]),[netTons]=IIF(cast(@netTons as decimal)!=0,@netTons ,[netTons]),[propulsionType]=IFNull(@propulsionType,[propulsionType]),[kw]=IFNull(@kw,[kw]),[shaftRPM]=IIF(cast(@shaftRPM as decimal)!=0,@shaftRPM ,[shaftRPM]),[propulsionManufacturer]=IFNull(@propulsionManufacturer,[propulsionManufacturer]),[gearManufacturer]=IFNull(@gearManufacturer,[gearManufacturer]),[classed]=IFNull(@classed ,[classed]),[classNo]=IFNull(@classNo,[classNo]),[classNotation]=IFNull(@classNotation,[classNotation]),[machinery]=IFNull(@machinery,[machinery]),[other]=IFNull(@other,[other]),[environment]=IFNull(@environment,[environment]),[firstVisitDate]=IFNull(@firstVisitDate,[firstVisitDate]),[lastVisitDate]=IFNull(@lastVisitDate,[lastVisitDate]),[SelectedPrefix]=IIF(@selectedPrefix!=0,@selectedPrefix,[SelectedPrefix])where [projectId]=@projectId";

            //public static readonly string Update_VesselMainData = $"{DbSchema}.Update_VesselDetails";


            public static readonly string Read_VesselMainData = $"SELECT * FROM ReportVesselMainData rt	where rt.projectId =@projectId ";

            //public static readonly string Read_VesselMainData = $"{DbSchema}.GetVesselMainData";

            public static readonly string Delete_VesselMainData = $"{DbSchema}.Delete_VesselMainData";
            //CRUD procedure for SurveyStatus

            public static readonly string Create_SurveyStatus = $"INSERT INTO ReportSurveyDetails ([projectId],[imoNum],[classSurveyDate],[dryDockDate],[specialContinuousMachineryDate],[boilerDate],[tailshaftDate],[specialContinuousHull1],[specialContinuousHull2],[specialContinuousHull3],[specialContinuousHull4],[specialContinuousHull5],[specialContinuousHull6],[specialContinuousHull7],[specialContinuousHull8],[specialContinuousHull9],[specialContinuousHull10],[specialContinuousHull11],[specialContinuousHull12],[specialContinuousHull13],[specialContinuousHull14],[specialContinuousHull15],[specialHullNo]) VALUES (@projectId,@imoNum,@classSurveyDate,@dryDockDate,@specialContinuousMachineryDate,@boilerDate,@tailshaftDate,@specialContinuousHull1,@specialContinuousHull2,@specialContinuousHull3,@specialContinuousHull4,@specialContinuousHull5,@specialContinuousHull6,@specialContinuousHull7,@specialContinuousHull8,@specialContinuousHull9,@specialContinuousHull10,@specialContinuousHull11,@specialContinuousHull12,@specialContinuousHull13,@specialContinuousHull14,@specialContinuousHull15,@specialHullNo)";

            //public static readonly string Create_SurveyStatus = $"{DbSchema}.Insert_SurveyDetails";

            public static readonly string Update_SurveyStatus = $"UPDATE ReportSurveyDetails SET [projectId]=@projectId,[classSurveyDate]=IFNULL(@classSurveyDate,[classSurveyDate]),[dryDockDate]=IFNULL(@dryDockDate,[dryDockDate]),[boilerDate]=IFNUll(@boilerDate,[boilerDate]),[specialContinuousMachineryDate]=IFNULL(@specialContinuousMachineryDate,[specialContinuousMachineryDate]),[tailshaftDate]=IFNUll(@tailshaftDate,[tailshaftDate]),[specialContinuousHull1] = IFNUll(@specialContinuousHull1,[specialContinuousHull1]),[specialContinuousHull2] =IFNUll( @specialContinuousHull2,[specialContinuousHull2]),[specialContinuousHull3] =IFNUll( @specialContinuousHull3,[specialContinuousHull3]),[specialContinuousHull4] =IFNUll( @specialContinuousHull4,[specialContinuousHull4]),[specialContinuousHull5] =IFNUll( @specialContinuousHull5,[specialContinuousHull5]),[specialContinuousHull6] =IFNUll( @specialContinuousHull6,[specialContinuousHull6]),[specialContinuousHull7] = IFNUll(@specialContinuousHull7,[specialContinuousHull7]),[specialContinuousHull8] =IFNUll( @specialContinuousHull8,[specialContinuousHull8]),[specialContinuousHull9] =IFNUll( @specialContinuousHull9,[specialContinuousHull9]),[specialContinuousHull10] =IFNUll( @specialContinuousHull10,[specialContinuousHull10]),[specialContinuousHull11] =IFNUll( @specialContinuousHull11,[specialContinuousHull11]),[specialContinuousHull12] =IFNUll( @specialContinuousHull12,[specialContinuousHull12]),[specialContinuousHull13] =IFNUll( @specialContinuousHull13,[specialContinuousHull13]),[specialContinuousHull14] = IFNUll(@specialContinuousHull14,[specialContinuousHull14]),[specialContinuousHull15] =IFNUll( @specialContinuousHull15,[specialContinuousHull15]),specialHullNo=IIF(@specialHullNo!=0 and @specialHullNo is not null,@specialHullNo,[specialHullNo]) where [projectId]=@projectId";

            //public static readonly string Update_SurveyStatus = $"{DbSchema}.Update_SurveyDetails";

            public static readonly string Read_SurveyStatus = $"Select * from ReportSurveyDetails Where projectId=@projectId";

            //public static readonly string Read_SurveyStatus = $"{DbSchema}.GetSurveyStatus";

            public static readonly string Delete_SurveyStatus = $"{DbSchema}.Delete_SurveyStatus";
            //CRUD procedure for StatutoryCertificates


            public static readonly string Create_Statutorycertificates = $"INSERT INTO ReportStatutoryCertificates ([projectId],[llliIssueDate],[llliExpiryDate],[safetyConIssueDate],[safetyConExpiryDate],[safetyRadioIssueDate],[safetyRadioExpiryDate],[safetyEquipmentIssueDate],[safetyEquipmentExpiryDate],[ioppIssueDate],[ioppExpiryDate],[iapIssueDate],[iapExpiryDate],[iafsIsuueDate],[iafsExpiryDate],[ismIssueDate],[ismExpiryDate],[isExpiryDate],[ispsIssueDate],[ispsExpiryDate],[cargoGearReTestIssueDate],[cargoGearReTestExpiryDate],[cargoGearAnnualIssueCertificate],[cargoGearAnnualExpiryCertificate],[ibwmIssueDate],[ibwmExpiryDate],[isppSewageIssueDate],[isppSewageExpiryDate],[ieeIssueDate],[ieeExpiryDate],[maritimeLabourIssueDate],[maritimeLabourExpiryDate]) VALUES (@projectId,@llliIssueDate,@llliExpiryDate,@safetyConIssueDate,@safetyConExpiryDate,@safetyRadioIssueDate,@safetyRadioExpiryDate,@safetyEquipmentIssueDate,@safetyEquipmentExpiryDate,@ioppIssueDate,@ioppExpiryDate,@iapIssueDate,@iapExpiryDate,@iafsIsuueDate,@iafsExpiryDate,@ismIssueDate,@ismExpiryDate,@isExpiryDate,@ispsIssueDate,@ispsExpiryDate,@cargoGearReTestIssueDate,@cargoGearReTestExpiryDate,@cargoGearAnnualIssueCertificate,@cargoGearAnnualExpiryCertificate,@ibwmIssueDate,@ibwmExpiryDate,@isppSewageIssueDate,@isppSewageExpiryDate,@ieeIssueDate,@ieeExpiryDate,@maritimeLabourIssueDate,@maritimeLabourExpiryDate)";

            //public static readonly string Create_Statutorycertificates = $"{DbSchema}.Insert_ReportStatutoryCertificates";


            public static readonly string Update_Statutorycertificates = $"UPDATE ReportStatutoryCertificates SET [projectId]=@projectId,[llliIssueDate]=IFNUll(@llliIssueDate,[llliIssueDate]),[llliExpiryDate]=IFNUll(@llliExpiryDate,[llliExpiryDate]),[safetyConIssueDate]=IFNUll(@safetyConIssueDate,[safetyConIssueDate]),[safetyConExpiryDate]=IFNUll(@safetyConExpiryDate,[safetyConExpiryDate]),[safetyRadioIssueDate]=IFNUll(@safetyRadioIssueDate,[safetyRadioIssueDate]),[safetyRadioExpiryDate]=IFNUll(@safetyRadioExpiryDate,[safetyRadioExpiryDate] ),[safetyEquipmentIssueDate]=IFNUll(@safetyEquipmentIssueDate ,[safetyEquipmentIssueDate]),[safetyEquipmentExpiryDate]=IFNUll(@safetyEquipmentExpiryDate,  [safetyEquipmentExpiryDate] ),[ioppIssueDate]=IFNUll(@ioppIssueDate,[ioppIssueDate] ),[ioppExpiryDate]=IFNUll(@ioppExpiryDate,[ioppExpiryDate]),[iapIssueDate]=IFNUll(@iapIssueDate,[iapIssueDate]),[iapExpiryDate]=IFNUll(@iapExpiryDate,[iapExpiryDate]),[iafsIsuueDate]=IFNUll(@iafsIsuueDate,[iafsIsuueDate]),[iafsExpiryDate]=IFNUll(@iafsExpiryDate,[iafsExpiryDate]),[ismIssueDate]=IFNUll(@ismIssueDate,[ismIssueDate]),[ismExpiryDate]=IFNUll(@ismExpiryDate,[ismExpiryDate]),[ispsIssueDate]=IFNUll(@ispsIssueDate,[ispsIssueDate]),[ispsExpiryDate]=IFNUll(@ispsExpiryDate,[ispsExpiryDate]),[cargoGearReTestIssueDate]=IFNUll(@cargoGearReTestIssueDate,[cargoGearReTestIssueDate]),[cargoGearReTestExpiryDate]=IFNUll(@cargoGearReTestExpiryDate,[cargoGearReTestExpiryDate]),[cargoGearAnnualIssueCertificate]=IFNUll(@cargoGearAnnualIssueCertificate,[cargoGearAnnualIssueCertificate]),[cargoGearAnnualExpiryCertificate]=IFNUll(@cargoGearAnnualExpiryCertificate,[cargoGearAnnualExpiryCertificate]),[ibwmIssueDate]=IFNUll(@ibwmIssueDate,[ibwmIssueDate]),[ibwmExpiryDate]=IFNUll(@ibwmExpiryDate,[ibwmExpiryDate]),[isppSewageIssueDate]=IFNUll(@isppSewageIssueDate,[isppSewageIssueDate]),[isppSewageExpiryDate]=IFNUll(@isppSewageExpiryDate,[isppSewageExpiryDate]),[ieeIssueDate]=IFNUll(@ieeIssueDate,[ieeIssueDate]),[ieeExpiryDate]=IFNUll(@ieeExpiryDate,[ieeExpiryDate]),[maritimeLabourIssueDate]=IFNUll(@maritimeLabourIssueDate,[maritimeLabourIssueDate]),[maritimeLabourExpiryDate]=IFNUll(@maritimeLabourExpiryDate,[maritimeLabourExpiryDate]) WHERE [projectId]=@projectId";

            //public static readonly string Update_Statutorycertificates = $"{DbSchema}.Update_ReportStatutoryCertificates";

            public static readonly string Read_Statutorycertificates = $"Select * from ReportStatutoryCertificates Where projectId=@projectId";

            //public static readonly string Read_Statutorycertificates = $"{DbSchema}.GetStatutoryCertificates";

            public static readonly string Delete_Statutorycertificates = $"{DbSchema}.Delete_Statutorycertificates";
        }

        public static class Port
        {
            public static readonly string Search = $"{Schema}.Port_Search";
        }
        public static class User
        {
            public static readonly string UserInfoByEmail = "SELECT id, FirstName, LastName, ProviderKey, EmailId FROM LogInData where EmailId=@email";
            public static readonly string UserInfo = "SELECT id, FirstName, LastName, ProviderKey, EmailId FROM LogInData";
        }

        public static class Class
        {
            public static readonly string Search = $"{Schema}.Class_Search";
        }

        public static class Comment
        {
            public static readonly string ReadCommentsByProjectId = $"{Schema}.Read_Comments_By_Project_Id";
            public static readonly string ReadCommentsByTaskId = $"{Schema}.Read_Comments_By_Task_Id";
            public static readonly string CreateComment = $"{Schema}.Create_Comment";
            public static readonly string DeleteComment = $"{Schema}.Delete_Comment";
            public static readonly string UpdateComment = $"{Schema}.Update_Comment";
        }

        public static class Grades
        {
            public static readonly string ReadProjectGrades = "SELECT Id, ProjectId, CAPCertificateNumber, CAPCertificateIssuanceDate, ClassReportNumber, ClassReportDate, StructuralReportNumber,StructuralReportDate, FinalGrade, StructuralGrade, FatigueGrade, RenewalGrade, MaterialGrade, GaugingGrade, HullGirderStrength, CAP1CertificateIssuanceDate, CAP1FinalGrade FROM ProjectGrades WHERE ProjectId = @ProjectId";


            //public static readonly string ReadProjectGrades= $"{Schema}.Read_Project_Grades";
            public static readonly string CreateProjectGrades = $"{Schema}.Create_Project_Grades";
            public static readonly string DeleteProjectGrades = $"{Schema}.Delete_Project_Grades";
            public static readonly string UpdateProjectGrades = $"{Schema}.Update_Project_Grades";
        }

        public static readonly string ReadCertsCount = $"SELECT json_group_array(json_object('Count', Count)) AS JSONResult FROM ( SELECT COUNT(p.[ID]) AS Count FROM ProjectTaskStatus pts INNER JOIN Projects p ON pts.[ProjectId] = p.[ID] WHERE pts.[TaskId] = 31 AND pts.[StatusId] = 1);";

        //public static readonly string ReadCertsCount = $"{Schema}.Read_Certs_Count";

        public static readonly string ReadCertsByRegion = $"SELECT json_group_array(json_object('Category', Category,'Value', Value,'Color', Color)) AS JSONResult FROM (SELECT p.[CapRegion] AS Category, COUNT(p.[ID]) AS Value, CASE  WHEN p.[CapRegion] = 'Americas/Europe' THEN 'rgb(0,47,108)' WHEN p.[CapRegion] = 'Americas / Europe' THEN 'rgb(0,47,108)' WHEN p.[CapRegion] = 'Singapore' THEN 'rgb(255,82,05)' WHEN p.[CapRegion] = 'Greece' THEN 'rgb(99,102,106)' ELSE 'rgb(0,133,173)' END AS Color FROM ProjectTaskStatus pts INNER JOIN Projects p ON pts.[ProjectId] = p.[ID] WHERE pts.[TaskId] = 31 AND pts.[StatusId] = 1 GROUP BY p.[CapRegion]) AS Result;";



        //public static readonly string ReadCertsByRegion = $"{Schema}.Read_Certs_ByRegion";

        public static readonly string ReadActivities = $"SELECT ID, Text, CreateBy, CreateDttm, ProjectId FROM Activities ORDER BY CreateDttm DESC LIMIT @Take";

        //public static readonly string ReadActivities = $"{Schema}.Read_Activities";


        public static readonly string ReadActivitiesByProjectId = $"SELECT Activities.ID, Activities.Text, Activities.CreateBy, Activities.CreateDttm, Projects.PIDNumber, Projects.ID AS 'ProjectId', Projects.VesselName FROM   Activities INNER JOIN Projects ON Activities.ProjectId = Projects.ID WHERE  Activities.ProjectId = @ProjectId ORDER BY CreateDttm DESC LIMIT @Take";

        //public static readonly string ReadActivitiesByProjectId = $"{Schema}.Read_Activities_ByProjectId";


        public static readonly string ReadCAPCoordinatorByRegion = $"{Schema}.Read_CAPCoordinator_ByRegion";

        public static class SurveyReport
        {
            public static readonly string GetReportsFromApplicationIds = "GetReportsFromApplicationIds";
            public static readonly string GetReport = "GetReport";
            public static readonly string CreateReport = "CreateReport";
            public static readonly string DeleteReport = "DeleteReport";
            public static readonly string GetSurveyReport = "GetSurveyReport";


            public static readonly string GetReportTemplateList = "SELECT Id, [Section Name] As 'Name' FROM ReportTemplate";

            //public static readonly string GetReportTemplateList = "GetReportTemplateList";

            public static readonly string GetReportTemplates = "GetReportTemplates";
            public static readonly string GetReportTemplateSectionId = "GetReportTemplateSectionId";


            public static readonly string GetExistingReportPartsSectionIds = "select TemplateId from ProjectTemplateMapping Where ProjectId=@ProjectId and IsDeleted=0";
            public static readonly string GetAllProject = "Select Id from Projects";

            //public static readonly string GetExistingReportPartsSectionIds = "GetExistingReportPartsSectionIds";

            public static readonly string GetProjectSectionsId = "GetProjectSectionIdList";
            public static readonly string GetVesselSurveyAndCertificates = "cap.GetVesselSurveyAndCertificates";
            public static readonly string ReadSurveyReportImages = $"{Schema}.Read_Survey_Report_Images";
            public static readonly string ReadSurveyReportUnplacedImages = $"{Schema}.Read_Survey_Report_Unplaced_Images";
            public static readonly string GetAssignmentIdsByReportId = $"upskill.sp_Get_AssignmentIds_ByReportId";
            public static readonly string GetAssignmentIdByProjectIdAndTemplateSectionId = $"upskill.sp_Get_AssignmentIds_ProjectIdAndReportSectionId";
            public static readonly string GetAllImagesByAssignmentId = $"[upskill].[sp_Get_All_Assignment_Images]";
            public static readonly string DeleteImagesByAssignmentId = $"[upskill].[sp_Delete_Assignment_Images]";
            public static readonly string GetPlacedImagesByProjectId = $"[upskill].[sp_Get_Placed_Images_ByProjectId]";
            public static readonly string GetImageByFileId = $"[upskill].[sp_Get_Image]";
            public static readonly string GetApplicationCardIdsByProjectId = $"[upskill].[sp_Get_Application_CardIds_ByProjectId]";
            public static readonly string GetReportNumberByProjectId = $"[upskill].[sp_Get_ReportNumber_ByProjectId]";
        }

        public static class BulkUpload
        {
            public static readonly string GetBulkUploadImage = "dbo.GetBulkUploadImage";
            public static readonly string InsertBulkUploadImage = "dbo.InsertBulkUploadImage";
            public static readonly string DeleteBulkUploadImage = "dbo.DeleteBulkUploadImage";
        }

        public static class ReportView
        {
            public static readonly string GetAllTemplateSections = "Select TemplateId ,SectionId  from ProjectGradingConditionMapping pgcm where IsSync is not NULL and IsDeleted =0 and ProjectId =@ProjectId Union Select TemplateId,SectionId  from ProjectGradingMapping pgm where IsSync is not NULL and IsDeleted =0 and ProjectId =@ProjectId Union Select TemplateId ,SectionId  From ProjectTemplateMapping ptm where IsSync is not NULL and IsDeleted =0 and ProjectId =@ProjectId Union Select TemplateId ,SectionId  From ProjectImageCards pic where IsSync is not NULL and IsDeleted =0 and ProjectId =@ProjectId  Union Select TemplateId ,SectionId from TankImageCard tic where IsSync is not NULL and ProjectId =@ProjectId Union Select TemplateId ,SectionId from GenericImageCard gic  where IsSync is not NULL and ProjectId =@ProjectId";
            public static readonly string GetTemplate = "select Id as Id,[Section Name] as SectionName from ReportTemplate where Id=@TemplateId";

            //public static readonly string GetTemplate = "dbo.GetTemplateTitle";

            public static readonly string GetTemplateSections = "select IIF(Sections.HasSubSection=1, sub.Id, Sections.Id) as Id ,Sections.Id as SectionId,Sections.SectionName as SectionName,Sections.HasSubSection,IIF(Sections.HasSubSection=1, sub.SectionName, Sections.SectionName) as SubSectionName,Sections.SpecialSectionName from TemplateSectionMapping join ReportTemplateSection as Sections on TemplateSectionMapping.SectionId=Sections.Id left outer join SubSection as sub on Sections.HasSubSection=1 and Sections.Id = sub.SectionId Where TemplateSectionMapping.TemplateId=@TemplateId";


            //public static readonly string GetTemplateSections = "dbo.GetTemplateSections";

            public static readonly string GetSectionGrading = "select GSMap.SectionId,grade.Id,grade.[LabelName]  from GradingSectionMapping as GSMap join Grading as grade on GSMap.GradingGroupId= grade.GradingGroupId and grade.ProjectId=@ProjectId and grade.IsActive=1 and grade.IsDeleted=0 Where GSMap.SectionId in (select IIF(Sections.HasSubSection=1, sub.Id, Sections.Id) as Id  from TemplateSectionMapping join ReportTemplateSection as Sections on TemplateSectionMapping.SectionId=Sections.Id left outer join SubSection as sub on Sections.HasSubSection=1 and Sections.Id = sub.SectionId Where TemplateSectionMapping.TemplateId=@TemplateId) order by GSMap.SectionId,grade.[LabelName]";

            //public static readonly string GetSectionGrading = "dbo.GetSectionGrading";


            public static readonly string GetGradingCondition = "select grading.Id as GradingId,gc.Condition,rts.Id as SectionId,gc.Id from Grading grading inner join GradingCondition gc on grading.GradingConditionGroupId=gc.GradingConditionGroupId inner join GradingSectionMapping gsm on grading.GradingGroupId=gsm.GradingGroupId inner join ReportTemplateSection rts on gsm.SectionId=rts.Id join TemplateSectionMapping tsm on  tsm.SectionId = rts.Id and tsm.TemplateId=@TemplateId where rts.HasSubSection!=1 and grading.ProjectId=@ProjectId";

            //public static readonly string GetGradingCondition = "dbo.GetGradingConditions";

            public static readonly string GetImageCards = "select * from ImageCard Where ImageCard.SectionId in (select IIF(Sections.HasSubSection=1, sub.Id, Sections.Id) as Id from TemplateSectionMapping join ReportTemplateSection as Sections on TemplateSectionMapping.SectionId=Sections.Id left outer join SubSection as sub on Sections.HasSubSection=1 and Sections.Id = sub.SectionId Where TemplateSectionMapping.TemplateId=@TemplateId)  order by id";

            //public static readonly string GetImageCards = "dbo.GetImageCards";

            public static readonly string GetCurrentCondition = "SELECT Id as Id, CurrentCondition as Name from CurrentCondition";

            //public static readonly string GetCurrentCondition = "dbo.GetCurrentCondition";

        }
        public static class DataTransfer
        {
            public static readonly string GetProject = "dbo.DataTransferProjects";
            public static readonly string GetDownloadedProject = "dbo.GetDownloadOfflineProject";
            public static readonly string AddDownloadedProject = "dbo.AddDownloadOfflineProject";
            public static readonly string MoveToHistoryTable = "dbo.MoveToHistoryTable";
            public static readonly string GetComments = "dbo.DataTransferComments";
            public static readonly string GetGrading = "dbo.DataTransferGrading";
            public static readonly string GetImageDescriptions = "dbo.DataTransferImageDescriptions";
            public static readonly string GetVessel_GradingMapping = "dbo.DataTransferVessel_GradingMapping";
            public static readonly string GetVessel_TankMapping = "dbo.DataTransferVessel_TankMapping";
            public static readonly string GetH_IReportTemplate = "dbo.GetH_IReportTemplateById";

            public static readonly string GetProjectGrading = "dbo.DataTranserProjectGradingMapping";
            public static readonly string GetProjectGradingConditionMapping = "dbo.DataTransferProjectGradingConditionMapping";
            public static readonly string GetProjectTemplateMapping = "dbo.DataTransferProjectTemplateMapping";

            public static readonly string GetTankImageCard = "dbo.DataTransferTankImageCard";
            public static readonly string GetImages = "dbo.DataTransferImages";
            public static readonly string GetImageData = "dbo.DataTransferImageData";
            public static readonly string GetProjectGrades = "dbo.DataTransferProjectGrades";
            public static readonly string ReleaseSyncData = "dbo.ReleaseSyncData";
            public static readonly string ReportVesselMainData = "dbo.GetReportVesselMainDataById";
            public static readonly string GetReportSurveyDetails = "dbo.GetReportSurveyDetailsById";
            public static readonly string GetReportStatutoryCertificates = "dbo.GetReportStatutoryCertificatesById";
            public static readonly string InsertActivities = "Insert Into Activities (Text,ProjectId,CreateBy,CreateDttm) Values (@Text,@ProjectId,@CreateBy,DATETIME('now'))";

        }
        public static class SyncDataTransfer
        {
            public static readonly string GetProject = "SELECT * FROM Projects WHERE ID = @ProjectId LIMIT 1;";
            public static readonly string GetExistingProject = "SELECT * FROM Projects WHERE ID <> @ProjectId LIMIT 1;";

            public static readonly string GetProjectBackUp = "SELECT * FROM Projects_backup WHERE ID = @ProjectId LIMIT 1;";
            public static readonly string GetDownloadedProject = "dbo.GetDownloadOfflineProject";
            public static readonly string AddDownloadedProject = "dbo.AddDownloadOfflineProject";
            public static readonly string GetComments = "dbo.DataTransferComments";

            public static readonly string GetImageDescriptions = "Select * from ImageDescriptions where ProjectId=@ProjectId and IsDeleted=0 and IsSync is not null";

            public static readonly string GetGrading = "Select * from Grading where ProjectId=@ProjectId and IsDeleted=0";


            public static readonly string GetVessel_GradingMapping = "Select * from Vessel_GradingMapping where ProjectId=@ProjectId and IsDeleted=0 and IsSync is not null";

            public static readonly string GetVessel_TankMapping = "Select * from Vessel_TankMapping where ImoNumber=@IMO  and IsSync is not null";
            public static readonly string GetVessel_TankMappingByProject = "Select * from Vessel_TankMapping where ImoNumber=@IMO and ProjectId=@ProjectId  and IsSync is not null";

            public static readonly string GetH_IReportTemplate = "Select * from H_IReportTemplate where ProjectId=@ProjectId and isSync is not null";
            public static readonly string GetHandIProjectGradings = "Select * from HandIProjectGrading where ProjectId=@ProjectId and isSync is not null";
            public static readonly string GetHandIProjectGradingConditions = "Select * from HandIProjectGradingConditions where ProjectId=@ProjectId and isSync is not null";

            public static readonly string GetProjectGrading = "select * from ProjectGradingMapping where ProjectId=@ProjectId and IsDeleted=0 and IsSync is not null";
            public static readonly string GetProjectGradingByTemplateId = "select * from ProjectGradingMapping where ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId  and IsDeleted=0 and IsSync is not null";

            public static readonly string GetProjectGradingConditionMapping = "Select * from ProjectGradingConditionMapping where ProjectId=@ProjectId and IsDeleted=0 and IsSync is not null";
            public static readonly string GetProjectGradingConditionMappingByTemplateId = "Select * from ProjectGradingConditionMapping where ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId and IsDeleted=0 and IsSync is not null";

            public static readonly string GetProjectTemplateMapping = "Select * from ProjectTemplateMapping where ProjectId=@ProjectId and IsDeleted=0 and IsSync is not null";
            public static readonly string GetProjectTemplateMappingByTemplateId = "Select * from ProjectTemplateMapping where ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId and IsDeleted=0 and IsSync is not null";


            public static readonly string GetTankImageCard = "Select * from TankImageCard where ProjectId=@ProjectId and IsSync is not null";
            public static readonly string GetTankImageCardByTemplateId = "Select * from TankImageCard where ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId   and IsSync is not null";

            public static readonly string GetProjectGrades = "Select * from ProjectGrades where ProjectId=@ProjectId";

            public static readonly string ReleaseSyncData = "dbo.ReleaseSyncData";
            public static readonly string ReportVesselMainData = "dbo.GetReportVesselMainDataById";
            public static readonly string GetReportSurveyDetails = "dbo.GetReportSurveyDetailsById";
            public static readonly string GetReportStatutoryCertificates = "dbo.GetReportStatutoryCertificatesById";


        }
        public static class ProjectMapping
        {

            public static readonly string GetProjectReportTemplate = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[Notes],[CreatedDttm],[UpdatedDttm],[IsDeleted] FROM ProjectTemplateMapping Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=IFNULL(@SectionId,SectionId) and IsDeleted=0";

            //public static readonly string GetProjectReportTemplate = "dbo.SelectProjectTemplateMapping";


            public static readonly string CreateProjectReportTemplate = "INSERT INTO ProjectTemplateMapping ([Id],[ProjectId],[TemplateId],[SectionId],[Notes],[CreatedDttm],[UpdatedDttm],[IsDeleted],[IsSync])VALUES (@Id,@ProjectId,@TemplateId,@SectionId,@Notes,@CreatedDttm,@UpdateDttm,@IsDeleted,@IsSync)";

            //public static readonly string CreateProjectReportTemplate = "dbo.CreateProjectTemplate";


            public static readonly string UpdateProjectReportTemplate = "UPDATE ProjectTemplateMapping SET [ProjectId]=@ProjectId,[TemplateId]=@TemplateId,[SectionId]=@SectionId,[Notes]=@Notes,[CreatedDttm]=@CreatedDttm,[UpdatedDttm]=@UpdateDttm,[IsDeleted]=@IsDeleted,[IsSync] = CASE WHEN [IsSync] IS NOT NULL  THEN [IsSync] ELSE 0 END Where Id=@Id";

            //public static readonly string GetImageCardById = "dbo.SelectProjectImagecardById";

            //public static readonly string GetProjectGrading = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[GradingId],[Value],[CreatedDttm],[UpdatedDttm],[IsDeleted]FROM ProjectGradingMapping Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=@SectionId and IsDeleted=0";

            public static readonly string GetProjectGrading = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[GradingId],SUBSTR(CASE WHEN LENGTH(Value) >= 20 AND INSTR(Value, ' ') = 0 THEN SUBSTR(Value, 1, LENGTH(Value) / 2) || ' ' || SUBSTR(Value, LENGTH(Value) / 2 + 1) ELSE Value END,1,20) AS Value,[CreatedDttm],[UpdatedDttm],[IsDeleted] FROM  ProjectGradingMapping WHERE  [ProjectId] = @ProjectId AND [TemplateId] = @TemplateId  AND [SectionId] = IFNULL(@SectionId,SectionId) AND IsDeleted = 0;";

            //public static readonly string GetProjectGrading = "dbo.SelectProjectGradingMapping";

            public static readonly string CreateProjectGrading = "INSERT INTO ProjectGradingMapping ([ProjectId],[TemplateId],[SectionId],[GradingId],[Value],[CreatedDttm] ,[UpdatedDttm],[IsDeleted],[IsSync]) VALUES  (@ProjectId,@TemplateId,@SectionId ,@GradingId,@Value,@CreatedDttm,@UpdateDttm,@IsDeleted,@IsSync)";

            //public static readonly string CreateProjectGrading = "dbo.CreateProjectGrading";


            public static readonly string UpdateProjectGrading = "UPDATE ProjectGradingMapping SET [ProjectId] = @ProjectId,[TemplateId] = @TemplateId,[SectionId] = @SectionId,[GradingId] = @GradingId,[Value] = @Value,[CreatedDttm] = @CreatedDttm,[UpdatedDttm] = @UpdateDttm,[IsDeleted] = @IsDeleted,[IsSync] = CASE WHEN [IsSync] IS NOT NULL  THEN [IsSync] ELSE 0 END WHERE Id=@Id";

            //public static readonly string UpdateProjectGrading = "dbo.UpdateProjectGrading";


            public static readonly string GetGradingbyId = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[GradingId],[Value],[CreatedDttm],[UpdatedDttm],[IsDeleted] FROM ProjectGradingMapping Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=IIF(@SectionId is not null,@SectionId,[SectionId]) and [GradingId]=IIF(@GradingId is not null,@GradingId,[GradingId]) and IsDeleted=0";

            //public static readonly string GetGradingbyId = "dbo.SelectProjectGradingCardById";



            public static readonly string GetProjectGradingCondition = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[GradingId],[GradingConditionLabel],[GradingConditionValue],[CreatedDttm],[UpdatedDttm],[IsDeleted] FROM ProjectGradingConditionMapping Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=@SectionId  and [GradingId]=@GradingId and IsDeleted=0";

            //public static readonly string GetProjectGradingCondition = "dbo.SelectProjectGradingConditionMapping";


            public static readonly string CreateProjectGradingCondition = "INSERT INTO ProjectGradingConditionMapping ([ProjectId],[TemplateId],[SectionId],[GradingId],[GradingConditionLabel],[GradingConditionValue],[CreatedDttm],[UpdatedDttm],[IsDeleted],[IsSync]) VALUES (@ProjectId,@TemplateId,@SectionId,@GradingId,@GradingConditionLabel,@GradingConditionValue,@CreatedDttm,@UpdateDttm,@IsDeleted,@IsSync)";

            //public static readonly string CreateProjectGradingCondition = "dbo.CreateProjectGradingConditionMapping";


            public static readonly string UpdateProjectGradingCondition = "UPDATE ProjectGradingConditionMapping SET  [ProjectId] = @ProjectId,[TemplateId] = @TemplateId,[SectionId] = @SectionId,[GradingId] = @GradingId,[GradingConditionLabel] = @GradingConditionLabel,[GradingConditionValue] = @GradingConditionValue,[CreatedDttm] = @CreatedDttm,[UpdatedDttm] = @UpdateDttm,[IsDeleted] = @IsDeleted,[IsSync] = CASE WHEN [IsSync] IS NOT NULL  THEN [IsSync] ELSE 0 END WHERE Id = @Id;";

            //public static readonly string UpdateProjectGradingCondition = "dbo.UpdateProjectGradingConditionMapping";


            public static readonly string GetGradingConditionbyId = "dbo.SelectProjectGradingConditionById";
            public static readonly string GetImagebyId = "dbo.GetImagebyId";

            public static readonly string DeleteReportTemplate = "dbo.DeleteProjectTemplate";

            public static readonly string CreateH_IReport = "INSERT INTO H_IReportTemplate ([Id],[ProjectId],[CompanyName],[CertificateNumber],[ExpiryDate],[ThicknessMeasurementA],[ThicknessMeasurementB],[ThicknessMeasurementC],[AdditionalSurveyorComments],[MaintenanceExecuted],[Accommodation],[PhotoGraphs],[OperationalTestLocation],[OperationalTestStart],[OperationalTestFinish],[OptionalTestSatisFactory],[AssociatedReportNumber],[AssociatedReportDate],[Remarks],[LeadSurveyorName],[LeadSurveyorNumber],[SurveyorName],[SurveyorNumber],[LastVisitDate],[IndividualA],[IndividualB],[IndividualC],[IndividualD],[IndividualE],[IndividualF],[SubstantialCorrosionRemaining],[StatutoryDeficiencies],[FoundSatisfactory],[UnderRemarks],[ScouringOrPitting],[BottomPlattingScouringOrPitting],[IsActive],[IsDeleted],[CreatedDttm],[UpdatedDttm],[OthersDescription],[IsSync]) VALUES (@Id,@ProjectId,@CompanyName,@CertificateNumber,@ExpiryDate,@ThicknessMeasurementA,@ThicknessMeasurementB,@ThicknessMeasurementC,@AdditionalSurveyorComments,@MaintenanceExecuted,@Accommodation,@PhotoGraphs,@OperationalTestLocation,@OperationalTestStart,@OperationalTestFinish,@OptionalTestSatisFactory,@AssociatedReportNumber,@AssociatedReportDate,@Remarks,@LeadSurveyorName,@LeadSurveyorNumber,@SurveyorName,@SurveyorNumber,@LastVisitDate,@IndividualA,@IndividualB,@IndividualC,@IndividualD,@IndividualE,@IndividualF,@SubstantialCorrosionRemaining,@StatutoryDeficiencies,@FoundSatisfactory,@UnderRemarks,@ScouringOrPitting,@BottomPlattingScouringOrPitting,@IsActive,@IsDeleted,@CreatedDttm,@UpdatedDttm,@OthersDescription,1)";

            //public static readonly string CreateH_IReport = "dbo.CreateH_IReport";

            public static readonly string GetH_IReport = "select * from H_IReportTemplate Where ProjectId=@projectId and IsActive=1 and IsDeleted=0";

            //public static readonly string GetH_IReport = "dbo.GetH_IReport";


            public static readonly string UpdateH_IReport = "update H_IReportTemplate set [CompanyName] = @CompanyName ,[CertificateNumber] = @CertificateNumber,[ExpiryDate] = @ExpiryDate,[ThicknessMeasurementA] = @ThicknessMeasurementA,[ThicknessMeasurementB] = @ThicknessMeasurementB,[ThicknessMeasurementC] = @ThicknessMeasurementC,[AdditionalSurveyorComments] = @AdditionalSurveyorComments,[MaintenanceExecuted] = @MaintenanceExecuted,[Accommodation] = @Accommodation,[PhotoGraphs] = @PhotoGraphs,[OperationalTestLocation] = @OperationalTestLocation,[OperationalTestStart] = @OperationalTestStart,[OperationalTestFinish] = @OperationalTestFinish,[OptionalTestSatisFactory] = @OptionalTestSatisFactory,[AssociatedReportNumber] = @AssociatedReportNumber,[AssociatedReportDate] = @AssociatedReportDate,[Remarks] = @Remarks,[LeadSurveyorName] = @LeadSurveyorName,[LeadSurveyorNumber] = @LeadSurveyorNumber,[SurveyorName] = @SurveyorName,[SurveyorNumber] = @SurveyorNumber,[LastVisitDate] = @LastVisitDate,[IndividualA] = @IndividualA,[IndividualB] = @IndividualB,[IndividualC] = @IndividualC,[IndividualD] = @IndividualD,[IndividualE] = @IndividualE,[IndividualF] = @IndividualF,[SubstantialCorrosionRemaining] = @SubstantialCorrosionRemaining,[StatutoryDeficiencies] = @StatutoryDeficiencies,[FoundSatisfactory] = @FoundSatisfactory,[UnderRemarks] = @UnderRemarks,[ScouringOrPitting] = @ScouringOrPitting,[BottomPlattingScouringOrPitting] = @BottomPlattingScouringOrPitting,[IsActive] = @IsActive,[IsDeleted] = @IsDeleted,[CreatedDttm] = @CreatedDttm,[UpdatedDttm] = @UpdatedDttm,[OthersDescription] = @OthersDescription,IsSync=0 WHERE ProjectId=@ProjectId and @IsActive=1 and @IsDeleted=0 and Id=@Id";

            //public static readonly string UpdateH_IReport = "dbo.UpdateH_IReport";
        }


        public static class Client
        {
            public static readonly string Update = $"{Schema}.Update_Client_ById";
        }

        public static class Tank
        {

            public static readonly string UpdateTankStatus = "UPDATE Vessel_TankMapping SET IsActive = @Status , IsSync=0 WHERE Id =@Id";

            //public static readonly string UpdateTankStatus = "dbo.UpdateTankStatus";

            public static readonly string GetTankType = "select  [TankType].Id,[TankType].TankType as TankName,[TankType].TankType as TankType  from TankType";
            public static readonly string GetTankTypeByVesselTankMapping = $"select distinct tt.Id, tt.TankType as TankName, tt.TankType as TankType from TankType tt Inner join VesselTypeTankSectionMapping vts on vts.TankTypeId = tt.id and vts.IsDeleted=0 Inner join VesselType vt on vt.Id = vts.VesselTypeId  where vt.VesselType=IFNULL(@Vesseltype,vt.VesselType)";
            public static readonly string GetTankTypeByProjectTankMapping = $"select distinct tt.Id, tt.TankType as TankName, tt.TankType as TankType from TankType tt Inner join ProjectTankSectionMapping vts on vts.TankTypeId = tt.id and vts.IsDeleted=0 where vts.projectid=@ProjectId union select distinct tt.Id, tt.TankType as TankName, tt.TankType as TankType from TankType tt inner join Vessel_TankMapping vts on vts.TankTypeId = tt.id and vts.IsDeleted=0  and IsActive=1 where vts.projectid=@ProjectId";
            //public static readonly string GetTankType = "dbo.GetTankType";


            public static readonly string GetTanksOfVessel = "SELECT DISTINCT vtm.Id AS TankId, vtm.Subheader as Subheader,COALESCE(vtm.VesselType, v.VesselType) AS VesselType, vtm.TankName, vtm.IsActive AS Status, vtm.ImoNumber, p.VesselName AS VesselName, tt.TankType AS TankType, vtm.IsDeleted, p.Name AS ProjectName FROM Vessel_TankMapping AS vtm INNER JOIN TankType AS tt ON vtm.TankTypeId = tt.Id INNER JOIN Projects AS p ON vtm.ProjectId = p.Id LEFT JOIN VesselType AS v ON p.ShipType = v.Id WHERE vtm.IsDeleted = 0 AND vtm.Id NOT IN (SELECT Id FROM Vessel_TankMapping WHERE VesselType IS NULL AND ImoNumber IS NULL) AND (@username IS NULL OR vtm.ImoNumber IN (SELECT DISTINCT IMO FROM Projects WHERE LeadSurveyor = @username OR SecondSurveyor LIKE '%' || @username || '%')) ORDER BY VesselType, TankType;";

            //public static readonly string GetTanksOfVessel = "dbo.Get_Vessel_Tank";


            public static readonly string GetTanksOfVesselById = "SELECT [Id],[VesselType],[ImoNumber],[TankTypeId],[TankName],[IsActive],[CreatedDttm],[UpdateDttm],[IsDeleted] FROM Vessel_TankMapping where Id=@Id";

            //public static readonly string GetTanksOfVesselById = "dbo.Get_Vessel_Tank_ById";

            public static readonly string GetTanksOfVesselByImoNumber = "dbo.Get_Vessel_Tank_ByIMONumber";
            public static readonly string GetTanksOfVesselByVesselType = "dbo.Get_Vessel_Tank_ByVesselType";
            public static readonly string GetTanksOfVesselByDetails = "dbo.Get_Vessel_Tank_ByDetails";
            public static readonly string GetTanksofVesselByProject = $"SELECT *  FROM [Vessel_TankMapping] Where [Imonumber]=@Imonumber and VesselType is null and IsDeleted=0 and ProjectId=@ProjectId";

            public static readonly string CreateTanksForVessel = "INSERT INTO Vessel_TankMapping ([Id],[VesselType],[Subheader],[ImoNumber],[TankTypeId],[TankName],[IsActive],[CreatedDttm],[UpdateDttm],IsDeleted,[IsSync],RequiredInReport,ProjectId) VALUES (@Id,@VesselType,@Subheader,@ImoNumber,@TankTypeId,@TankName,@IsActive,@CreatedDttm,@UpdateDttm,@IsDeleted,1,  (SELECT RequiredInReport FROM Vessel_TankMapping            WHERE ImoNumber = @ImoNumber  AND  ProjectId= @ProjectId            AND TankTypeId = @TankTypeId              AND IsActive = 1              AND IsDeleted = 0            LIMIT 1),@ProjectId )";

            public static readonly string CreateProjectTank = "INSERT INTO Vessel_TankMapping   ([Id], [VesselType], [ImoNumber], [TankTypeId], [TankName], [IsActive], [CreatedDttm], [UpdateDttm], [IsDeleted], [IsSync], [RequiredInReport])  VALUES   (@Id, @VesselType, @ImoNumber, @TankTypeId, @TankName, @IsActive, @CreatedDttm, @UpdateDttm, @IsDeleted, 1,           (SELECT RequiredInReport FROM Vessel_TankMapping            WHERE ImoNumber = @ImoNumber              AND TankTypeId = @TankTypeId              AND IsActive = 1              AND IsDeleted = 0            LIMIT 1)  );";

            //public static readonly string CreateTanksForVessel = "dbo.Create_Vessel_TankMapping";

            public static readonly string UpdateTankOfVessel = "UPDATE Vessel_TankMapping SET [VesselType] =@VesselType,[ImoNumber] =@ImoNumber,[TankTypeId]=@TankTypeId,[TankName]=@TankName,[IsActive]=@IsActive,[CreatedDttm]=@CreatedDttm,[UpdateDttm]=@UpdateDttm,IsDeleted= @IsDeleted,IsSync=0,Subheader=@Subheader Where Id=@Id";

            //public static readonly string UpdateTankOfVessel = "dbo.Update_Vessel_TankMapping";

            public static readonly string CreateVesselGrading = "dbo.Create_Vessel_Grading";
            public static readonly string UpdateVesselGrading = "dbo.Update_Vessel_Grading";

            public static readonly string GetVesselGrading = "dbo.Get_Vessel_Tank_Grading";
            public static readonly string GetVesselGradingById = "dbo.Get_Vessel_Tank_Grading_ById";
            public static readonly string GetVesselGradingByImoNumber = "dbo.Get_Vessel_Tank_Grading_By_IMONumber";
            public static readonly string GetVesselGradingByVesselType = "dbo.Get_Vessel_Tank_Grading_ByVesselType";
            public static readonly string GetVesselGradingByDetails = "dbo.Get_Vessel_Tank_Grading_By_Details";

            public static readonly string GetProjectTankImageCard = "select DISTINCT TankImageCard.Id,CardName,CardNumber,DescriptionId,AdditionalDescription,CurrentCondition,TankImageCard.ImageId,IsActive,Base64String as src from TankImageCard  left outer join ImageData as ImageData on TankImageCard.ImageId = ImageData.ImageId Where TankImageCard.ProjectId=@ProjectId and TemplateId=@TemplateId and SectionId=@SectionId and IsActive=1";

            //public static readonly string GetProjectTankImageCard = "dbo.GetTankImageCard";

            public static readonly string GetProjectTankImageCardById = "SELECT [Id],[ProjectId],[TemplateId],[SectionId],[CardNumber],[DescriptionId],[AdditionalDescription],[CurrentCondition],[ImageId],[CreatedDttm],[UpdatedDttm],[IsActive],[CardName] FROM TankImageCard Where [ProjectId]=@ProjectId and [TemplateId]=@TemplateId and [SectionId]=IIF(@SectionId is not null,@SectionId,[SectionId]) and [CardNumber]=@CardNumber and IsActive=1";

            //public static readonly string GetProjectTankImageCardById = "dbo.GetProjectTankImageCardByName";


            public static readonly string CreateProjectTankImageCard = "INSERT INTO TankImageCard ([ProjectId],[TemplateId],[SectionId],[CardNumber],[DescriptionId],[AdditionalDescription],[CurrentCondition],[ImageId],[CreatedDttm],[UpdatedDttm],[IsActive],[CardName],[IsSync]) VALUES (@ProjectId,@TemplateId,@SectionId,@CardNumber,@DescriptionId,@AdditionalDescription,@CurrentCondition,@ImageId,@CreatedDttm,@UpdatedDttm,@IsActive,@CardName,@IsSync)";

            //public static readonly string CreateProjectTankImageCard = "dbo.CreateTankImageCard";


            public static readonly string UpdateProjectTankImageCard = "UPDATE TankImageCard SET [ProjectId] = @ProjectId,[TemplateId] = @TemplateId,[SectionId] =IIF(@SectionId is null,null,@SectionId),[CardNumber] = @CardNumber,[DescriptionId] = @DescriptionId,[AdditionalDescription] = @AdditionalDescription,[CurrentCondition] = @CurrentCondition,[ImageId] = @ImageId,[CreatedDttm] = @CreatedDttm,[UpdatedDttm] = @UpdatedDttm,[IsActive] = @IsActive,[CardName] = @CardName,[IsSync] = CASE WHEN [IsSync] IS NOT NULL  THEN [IsSync] ELSE 0 END WHERE Id=@Id";

            //public static readonly string UpdateProjectTankImageCard = "dbo.UpdateTankImageCard";

            public static readonly string GetTankGradingConditionById = "select GradingCondition.Id,Condition as Name from GradingCondition inner join [Vessel_GradingMapping] on GradingCondition.GradingConditionGroupId=[Vessel_GradingMapping].GradingConditionGroupId Where [Vessel_GradingMapping].Id=@Id";

            //public static readonly string GetTankGradingConditionById = "dbo.GetTankGradingConditionById";

            public static readonly string GetTempaleTankGrading = "select vgm.Id,Gradingname as Name,GradingConditionGroupId,RequiredInReport,TankTypeId from Vessel_GradingMapping  vgm Where VesselType=@VesselType and vgm.ProjectId=@ProjectId and  IsActive=1 and vgm.IsDeleted=0 and TankTypeId = COALESCE( CASE WHEN @TankTypeId = 0 THEN NULL ELSE @TankTypeId END, TankTypeId ) Order By TankTypeId,GradingName";

            //public static readonly string GetTempaleTankGrading = "dbo.GetTempaleTankGrading";

            public static readonly string GetTempaleTanks = "SELECT distinct Vessel_TankMapping.Id, Vessel_TankMapping.Subheader,TankName, TankTypeId, TankType.TemplateId, TankType.CurrentconditionPlaceholders FROM Vessel_TankMapping INNER JOIN TankType ON Vessel_TankMapping.TankTypeId = TankType.Id AND TemplateId = @TemplateId and IsDeleted=0 and IsActive=1 WHERE ProjectId=IFNULL(@ProjectId,ProjectId) and ((@VesselType IS NOT NULL AND ((SELECT COUNT(*) FROM Vessel_TankMapping WHERE VesselType = @VesselType AND IsActive = 1) > 0 AND VesselType = @VesselType)) OR (@ImoNumber IS NOT NULL AND ((SELECT COUNT(*) FROM Vessel_TankMapping WHERE ImoNumber = @ImoNumber AND IsActive = 1) > 0 AND ImoNumber = @ImoNumber AND (SELECT COUNT(*) FROM Vessel_TankMapping WHERE VesselType = @VesselType AND IsActive = 1) = 0))) ORDER BY TankTypeId, TankName;";

            //public static readonly string GetTempaleTanks = "dbo.GetTempaleTanks";

            public static readonly string GetImageDescriptionById = "dbo.GetImageDescriptionById";

            public static readonly string GetImoNumber = "Select DISTINCT IMO,VesselName from Projects";

            //public static readonly string GetImoNumber = "dbo.GetIMONumber";

            public static readonly string GetVesselType = "dbo.GetVesselType";

            public static readonly string GetShipType = "select * from VesselType";

            //public static readonly string GetShipType = "dbo.GetShipType";


            public static readonly string GetDeafultTanks = "dbo.GetDefaultTank";
            public static readonly string GetDefaultGradings = "dbo.GetDefaultGrading";


            public static readonly string GetVesselTypeList = "dbo.GetVesselTypeList";





        }

        public static class Grading
        {
            public static readonly string GetAllGrading = "dbo.GetAllGrading";
            public static readonly string GetGradingTemplate = "dbo.GetGradingTemplate";
            public static readonly string GetGradingSections = "dbo.GetGradingSections";
            public static readonly string GetSectionGradings = "dbo.GetSectionGrading";
            public static readonly string CreateSectionGrading = "dbo.CreateSectionGrading";
            public static readonly string UpdateSectionGrading = "dbo.UpdateSectionGrading";
            public static readonly string GetTemplateGradingbyImo = "dbo.GetTemplateSectionsGradingByImoNumber";
            public static readonly string GetTemplateGradingbyVesselType = "dbo.GetTemplateSectionsGradingByVesselType";
            public static readonly string GetDefaultTemplateGrading = "dbo.GetDefaultTemplateSectionGrading";
            public static readonly string CreateTankGrading = "dbo.CreateTankGrading";
            public static readonly string UpdateTankGrading = "dbo.UpdateTankGrading";
            public static readonly string GetGradingByGradingId = "dbo.GetGradingByGradingId";
        }

        public static class ImageDescription
        {
            public static readonly string GetAllDescription = "dbo.GetAllDescription";
            public static readonly string CreateImageDescription = "dbo.CreateImageDescription";
            public static readonly string UpdateImageDescription = "dbo.UpdateImageDescription";
            public static readonly string GetImageDescriptionById = "GetImageDescriptionById";

            public static readonly string GetImageDescriptionsByProjectId = "select * from (select descr.Id,descr.Description,descr.CategoryId,descr.ProjectId,descr.IsActive,descr.IsDeleted,rts.Id as SectionId,rts.SectionName,descr.CreatedDttm,descr.UpdatedDttm,NULL as TankTypeId from ImageDescriptions descr  inner join Section_ImageDescriptionMapping secimg on descr.CategoryId =secimg.CategoryId inner join ReportTemplateSection rts on rts.Id = secimg.SectionId  where ProjectId=@projectId and IsActive=1) SectionGrading union all select * from (select descr.Id,descr.Description,descr.CategoryId,descr.ProjectId,descr.IsActive,descr.IsDeleted,NULL as SectionId,tt.TankType as SectionName,descr.CreatedDttm,descr.UpdatedDttm,tt.Id as TankTypeId from ImageDescriptions descr  inner join Tank_ImageDescriptionMapping tank on descr.CategoryId =tank.CategoryId inner join TankType tt on tank.TankTypeId = tt.Id where ProjectId=@projectId and IsActive=1)TankGrading order by Id";

            //public static readonly string GetImageDescriptionsByProjectId = "GetImageDescriptionsByProjectId";

            public static readonly string GetImageDescriptionsByVesselType = "GetImageDescriptionsByVesselType";

        }
        public class Section
        {
            public static readonly string GetGradingBysectionId = "dbo.GetSectionGradingBySectionId";
            public static readonly string GetGradingConditionBySectionId = "dbo.GetSectionGradingConditionBySectionId";
            public static readonly string GetImageCardBySectionId = "dbo.GetImageCardsBySectionId";
            public static readonly string GetSectionNameBySecionId = "dbo.GetSectionNameBySectionId";

            public static readonly string GetTankSectionGradingConditionBySectionId = "dbo.GetTankSectionGradingConditionBySectionId";
        }

        public class UserPermisson
        {
            public static readonly string GetUserPermission = "dbo.GetUserPermissions";
            public static readonly string InsertUserPermission = "dbo.Insert_UserPermissions";
            public static readonly string UpdatetUserPermission = "dbo.Update_UserPermissions";
            public static readonly string DeleteUserPermission = "dbo.Delete_UserPermissions";

        }

        public class RolePermission
        {
            public static readonly string GetAllPermissions = "[dbo].[GetAllCurrentUserPermission]";
            public static readonly string GetCurrentUserProject = "dbo.GetCurrentUserProjects";
            public static readonly string GetUserRolePermission = "dbo.GetUserRolePermission";
            public static readonly string InsertUserRolePermission = "dbo.Insert_RolePermissionMapping";
            public static readonly string UpdatetUserRolePermission = "dbo.Update_RolePermissionMapping";
            public static readonly string DeleteUserRolePermission = "dbo.Delete_RolePermissionMapping";
            public static readonly string GetAspNetUserRoles = "dbo.GetAspNetUserRoles";
            public static readonly string GetRolePermissionByUserName = "dbo.GetCurrentUserPermission";
            public static readonly string GetRolePermissionById = "dbo.GetRolePermissionById";
        }
    }
}
