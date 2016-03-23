using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.FileContainer;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public static class FileContainerClientExtension
    {
        public static async Task<HttpResponseMessage> UploadFileAsync(
            this FileContainerHttpClient client,
            Int64 containerId,
            String itemPath,
            Stream fileStream,
            Guid scopeIdentifier,
            CancellationToken cancellationToken = default(CancellationToken),
            int chunkSize = 16 * 1024 * 1024,
            bool uploadFirstChunk = false,
            Object userState = null,
            Boolean compressStream = true)
        {
            int i = 5;
            while (i-- > 0)
            {
                await Task.Delay(1000, cancellationToken);
            }

            return await Task.FromResult<HttpResponseMessage>(new HttpResponseMessage(System.Net.HttpStatusCode.Created));
        }
    }
    public class FileContainerHelper
    {
        private readonly ConcurrentQueue<string> fileUploadQueue = new ConcurrentQueue<string>();
        private SemaphoreSlim _fileUploadSerializer;
        private static readonly int _httpRetryCount = 3;

        private Uri ProjectCollectionUrl { get; }
        private VssCredentials Credential { get; }
        private Guid ProjectId { get; }
        private Int64 ContainerId { get; }
        private string ContainerPath { get; }
        private string SourceParentDirectory { get; set; }

        private FileContainerHttpClient FolderCreateHttpClient { get; }
        private FileContainerHttpClient FileUploadHttpClient { get; }

        public FileContainerHelper(
            Uri projectCollectionUrl,
            VssCredentials credential,
            Guid projectId,
            Int64 containerId,
            String containerPath)
        {
            ArgUtil.NotNull(projectCollectionUrl, nameof(projectCollectionUrl));
            ArgUtil.NotNull(credential, nameof(credential));
            ArgUtil.NotEmpty(projectId, nameof(projectId));
            ArgUtil.NotNullOrEmpty(ContainerPath, nameof(ContainerPath));

            ProjectCollectionUrl = projectCollectionUrl;
            Credential = credential;
            ProjectId = projectId;
            ContainerId = containerId;
            ContainerPath = containerPath;

            // default folder creation request timeout to 100 seconds, that is the standard
            // TODO: Load from .ini file.
            VssHttpRequestSettings folderCreateRequestSettings = new VssHttpRequestSettings();
            folderCreateRequestSettings.SendTimeout = TimeSpan.FromSeconds(100);
            FolderCreateHttpClient = new FileContainerHttpClient(
               ProjectCollectionUrl,
               Credential,
               folderCreateRequestSettings,
               new VssHttpRetryMessageHandler(_httpRetryCount));

            // default file upload request timeout to 300 seconds, that is the standard
            // TODO: Load from .ini file.
            VssHttpRequestSettings fileUploadRequestSettings = new VssHttpRequestSettings();
            fileUploadRequestSettings.SendTimeout = TimeSpan.FromSeconds(300);
            FileUploadHttpClient = new FileContainerHttpClient(
                ProjectCollectionUrl,
                Credential,
                fileUploadRequestSettings,
                new VssHttpRetryMessageHandler(_httpRetryCount));
        }

        public async Task CopyToContainerAsync(
            IExecutionContext context,
            String source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // create the container folder to ensure it exists for downloading
            var folder = new FileContainerItem();
            folder.ContainerId = ContainerId;
            folder.ItemType = ContainerItemType.Folder;
            folder.Path = ContainerPath;

            await FolderCreateHttpClient.CreateItemsAsync(ContainerId, new List<FileContainerItem>() { folder }, ProjectId, cancellationToken);

            // start uploading files
            int maxConcurrentUploads = Math.Min(Environment.ProcessorCount, 8);
            context.Debug($"Max Concurrent Uploads {maxConcurrentUploads}");

            //WinHttpHandler.MaxConnectionsPerServer

            IEnumerable<String> files;
            if (File.Exists(source))
            {
                files = new List<String>() { source };
                SourceParentDirectory = Path.GetDirectoryName(source);
            }
            else
            {
                files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories);
                SourceParentDirectory = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            for (int i = 0; i < maxConcurrentUploads; i++)
            {
                _parallelUploadTasks.Add(ParallelUpload(context));
            }

            _fileUploadSerializer = new SemaphoreSlim(0, files.Count());
            foreach (var file in files)
            {
                fileUploadQueue.Enqueue(file);
                _fileUploadSerializer.Release();
            }

            await ReportingAsync(files.Count());
        }

        private async Task ParallelUpload(IExecutionContext context)
        {
            while (true)
            {
                await Task.WhenAny(_fileUploadSerializer.WaitAsync(), _allFileUploaded.Task);

                if (_allFileUploaded.Task.IsCompleted)
                {
                    // all files been uploaded.
                    break;
                }

                string fileToUpload;
                if (fileUploadQueue.TryDequeue(out fileToUpload))
                {
                    Stopwatch watch = new Stopwatch();
                    try
                    {
                        watch.Start();
                        using (FileStream fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read))
                        {
                            string itemPath = (ContainerPath.TrimEnd('/') + "/" + fileToUpload.Remove(0, SourceParentDirectory.Length + 1)).Replace('\\', '/');
                            var response = await FileUploadHttpClient.UploadFileAsync(ContainerId, itemPath, fs, ProjectId, default(CancellationToken));
                            if (response.StatusCode != System.Net.HttpStatusCode.Created)
                            {
                                throw new Exception($"Unable to copy file to server StatusCode={response.StatusCode}: {response.ReasonPhrase}. Source file path: {fileToUpload}. Target server path: {itemPath}");
                            }
                        }
                    }
                    finally
                    {
                        watch.Stop();

                        if (watch.Elapsed > m_SlowCallThreshold)
                        {
                            context.Debug($"File {fileToUpload} took more than {watch.Elapsed.Seconds} seconds to upload.");
                        }
                    }

                    Interlocked.Increment(ref m_filesUploaded);
                }
            }
        }

        private async Task ReportingAsync(int totalFiles)
        {
            if (m_filesUploaded == totalFiles)
            {
                _allFileUploaded.SetResult(0);
                await Task.WhenAll(_parallelUploadTasks);
            }
        }

        private readonly TaskCompletionSource<int> _allFileUploaded = new TaskCompletionSource<int>();
        private static TimeSpan m_SlowCallThreshold = TimeSpan.FromSeconds(30);
        private int m_filesUploaded = 0;
        private List<Task> _parallelUploadTasks = new List<Task>();
    }
}