using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Helpers.Constants;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalCap.Infrastructure.Service
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IFileStorageRepository _fileStorageRepository;
        private readonly IBlobStorageRepository _blobStorageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public FileStorageService(
            IFileStorageRepository fileStorageRepository,
            IBlobStorageRepository blobStorageRepository,
            IUnitOfWork unitOfWork,
            IEmailService emailService)
        {
            _fileStorageRepository = fileStorageRepository;
            _blobStorageRepository = blobStorageRepository;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<IEnumerable<ProjectFile>> GetProjectFiles(int projectId)
        {
            return await _fileStorageRepository.GetProjectFiles(projectId);
        }

        public async Task<IEnumerable<ProjectFile>> GetTaskFiles(int taskId)
        {
            return await _fileStorageRepository.GetTaskFiles(taskId);
        }

        public async Task<ProjectFile> SaveProjectFile(IFormFile uploadedFile, FileDataType dataType, int projectId, string currentUser)
        {
            if (uploadedFile == null)
                throw new ArgumentNullException(nameof(uploadedFile));

            var file = new ProjectFile(uploadedFile)
            {
                ProjectId = projectId,
                DataType = dataType
            };

            var storageKey = await _blobStorageRepository.SaveFile(uploadedFile);
            var savedFile = await _fileStorageRepository.SaveProjectFile(file, storageKey, currentUser);
            return savedFile;
        }

        public async Task<ProjectFile> SaveTaskFile(IFormFile uploadedFile, FileDataType dataType, int projectId, int taskId, string currentUser)
        {
            if (uploadedFile == null)
                throw new ArgumentNullException(nameof(uploadedFile));

            var file = new ProjectFile(uploadedFile)
            {
                ProjectId = projectId,
                DataType = dataType
            };

            var storageKey = await _blobStorageRepository.SaveFile(uploadedFile);
            var savedFile = await _fileStorageRepository.SaveTaskFile(file, storageKey, taskId, currentUser);
            return savedFile;
        }

        public async Task DeleteFile(int fileId, string currentUser)
        {
            var file = await _fileStorageRepository.GetFile<ProjectFile>(fileId);

            if (file != null && !string.IsNullOrEmpty(file.StorageKey))
            {
                await _blobStorageRepository.DeleteFile(file.StorageKey);
            }

            await _fileStorageRepository.DeleteFile(fileId, currentUser);
        }

        public async Task<string> GetFileDownloadUrl(int fileId)
        {
            var file = await _fileStorageRepository.GetFile<ProjectFile>(fileId);
            if (file == null || string.IsNullOrEmpty(file.StorageKey))
                return null;

            return await _blobStorageRepository.GetFileDownloadUrl(file.StorageKey);
        }

        public async Task<T> GetFile<T>(int fileId) where T : ProjectFile, new()
        {
            return await _fileStorageRepository.GetFile<T>(fileId);
        }

        public async Task MoveFile(int fileId, FileDataType fileDataType, string currentUser)
        {
            await _fileStorageRepository.MoveFile(fileId, fileDataType, currentUser);
        }

        public async Task UpdateFileMetadata(int id, FileDataType dataType, string name, bool availableToClient, string currentUser)
        {
            await _fileStorageRepository.UpdateFileMetadata(id, dataType, name, availableToClient, currentUser);
        }

        public async Task ShareFile(int projectId, string projectName, string emailTo, string fileUrl, string fileName)
        {
            var subject = $"File Shared: {fileName}";
            var body = $"A file has been shared with you from project {projectName}.\n\nFile: {fileName}\n\nAccess the file here: {fileUrl}";
            var htmlBody = $"<p>A file has been shared with you from project <strong>{projectName}</strong>.</p><p><strong>File:</strong> {fileName}</p><p><a href=\"{fileUrl}\">Access the file here</a></p>";

            var result = await _emailService.SendEmailAsync(new[] { emailTo }, subject, body, htmlBody);
            if (!result.IsSuccess)
            {
                throw new Exception($"Failed to send email: {result.ErrorMessage}");
            }
        }

        public async Task<MemoryStream> DownloadZip(int projectId, FileDataType fileDataType, bool clientDeliverablesOnly)
        {
            var files = await _fileStorageRepository.GetProjectFiles(projectId);

            if (clientDeliverablesOnly)
            {
                files = files.Where(f => f.AvailableToClient && f.DataType == FileDataType.ClientDeliverables);
            }
            else
            {
                files = files.Where(f => f.DataType == fileDataType);
            }

            return await _blobStorageRepository.GetZipAsync(files);
        }
    }
}
