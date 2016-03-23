using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public class ArtifactCommands : AgentService, ICommandExtension
    {
        public Type ExtensionType
        {
            get
            {
                return typeof(ICommandExtension);
            }
        }

        public String CommandArea
        {
            get
            {
                return "artifact";
            }
        }

        public void ProcessCommand(IExecutionContext context, Command command)
        {
            if (String.Equals(command.Event, WellKnownArtifactCommand.Associate, StringComparison.OrdinalIgnoreCase))
            {
                ProcessArtifactAssociateCommand(context, command.Properties, command.Data);
            }
            else if (String.Equals(command.Event, WellKnownArtifactCommand.Upload, StringComparison.OrdinalIgnoreCase))
            {
                ProcessArtifactUploadCommand(context, command.Properties, command.Data);
            }
            else
            {
                throw new Exception($"##vso[artifact.{command.Event}] is not a recognized command for Artifact command extension. TODO: DOC aka link");
            }

            return;
        }

        private void ProcessArtifactAssociateCommand(IExecutionContext context, Dictionary<string, string> eventProperties, string data)
        {
            ArgUtil.NotNull(context, nameof(context));
            ArgUtil.NotNull(context.Endpoints, nameof(context.Endpoints));

            ServiceEndpoint systemConnection = context.Endpoints.FirstOrDefault(e => string.Equals(e.Name, ServiceEndpoints.SystemVssConnection, StringComparison.OrdinalIgnoreCase));

            ArgUtil.NotNull(systemConnection, nameof(systemConnection));
            ArgUtil.NotNull(systemConnection.Url, nameof(systemConnection.Url));

            Uri projectUrl = systemConnection.Url;
            VssCredentials projectCredential = ApiUtil.GetVssCredential(systemConnection);
            Guid projectId = context.Variables.System_TeamProjectId ?? Guid.Empty;

            ArgUtil.NotEmpty(projectId, nameof(projectId));

            int? buildId = context.Variables.Build_BuildId;
            ArgUtil.NotNull(buildId, nameof(buildId));

            string artifactName;
            if (!eventProperties.TryGetValue(ArtifactAssociateEventProperties.ArtifactName, out artifactName))
            {
                throw new Exception("Artifact Name is required.");
            }

            String artifactLocation = data;
            if (!String.IsNullOrEmpty(artifactLocation))
            {
                string artifactType;
                if (!eventProperties.TryGetValue(ArtifactAssociateEventProperties.ArtifactType, out artifactType))
                {
                    artifactType = InferArtifactResourceType(context, artifactLocation);
                }

                if (String.IsNullOrEmpty(artifactType))
                {
                    throw new Exception("Artifact Type is required.");
                }

                var propertyDictionary = ExtractArtifactProperties(eventProperties);

                String artifactData = "";
                if (IsContainerPath(artifactLocation) ||
                    IsValidServerPath(artifactLocation))
                {
                    //if artifactlocation is a file container path or a tfvc server path
                    artifactData = artifactLocation;
                }
                else if (IsUncSharePath(context, artifactLocation))
                {
                    //if artifactlocation is a UNC share path
                    artifactData = new Uri(artifactLocation).LocalPath;
                }
                else
                {
                    throw new Exception($"Unsupport artifact location: {artifactLocation}");
                }

                // this block in the queue
                AsyncCommandContext commandContext = new AsyncCommandContext(context, ArtifactTaskName.AssociateArtifact);
                Task associateArtifactTask = AssociateArtifactAsync(commandContext, projectUrl, projectCredential, projectId, buildId.Value, artifactName, artifactType, artifactData, propertyDictionary, context.CancellationToken);
                commandContext.Task = associateArtifactTask;

                ExecutionContext executionContext = context as ExecutionContext;
                executionContext.AsyncCommands.Add(commandContext);
                //
            }
            else
            {
                throw new Exception("Cannot associate artifact, artifact location is not specified.");
            }
        }

        private async Task AssociateArtifactAsync(
            AsyncCommandContext context,
            Uri projectCollection,
            VssCredentials credentials,
            Guid projectId,
            Int32 buildId,
            String name,
            String type,
            String data,
            Dictionary<String, String> propertiesDictionary,
            CancellationToken cancellationToken)
        {
            BuildHelper buildHelper = new BuildHelper();
            var artifact = await buildHelper.AssociateArtifact(projectCollection, credentials, projectId, buildId, name, type, data, propertiesDictionary, cancellationToken);
            context.Output($"Associated artifact {artifact.Id} with build {buildId}");
        }

        private void ProcessArtifactUploadCommand(IExecutionContext context, Dictionary<string, string> eventProperties, string data)
        {
            ArgUtil.NotNull(context, nameof(context));
            ArgUtil.NotNull(context.Endpoints, nameof(context.Endpoints));

            ServiceEndpoint systemConnection = context.Endpoints.FirstOrDefault(e => string.Equals(e.Name, ServiceEndpoints.SystemVssConnection, StringComparison.OrdinalIgnoreCase));

            ArgUtil.NotNull(systemConnection, nameof(systemConnection));
            ArgUtil.NotNull(systemConnection.Url, nameof(systemConnection.Url));

            Uri projectUrl = systemConnection.Url;
            VssCredentials projectCredential = ApiUtil.GetVssCredential(systemConnection);
            Guid projectId = context.Variables.System_TeamProjectId ?? Guid.Empty;

            ArgUtil.NotEmpty(projectId, nameof(projectId));

            int? buildId = context.Variables.Build_BuildId;
            ArgUtil.NotNull(buildId, nameof(buildId));

            long? containerId = context.Variables.Build_ContainerId;
            ArgUtil.NotNull(containerId, nameof(containerId));

            string artifactName;
            if (!eventProperties.TryGetValue(ArtifactAssociateEventProperties.ArtifactName, out artifactName))
            {
                throw new Exception("Artifact Name is required.");
            }

            string containerFolder;
            if (!eventProperties.TryGetValue(ArtifactUploadEventProperties.ContainerFolder, out containerFolder))
            {
                containerFolder = artifactName;
            }

            var propertyDictionary = ExtractArtifactProperties(eventProperties);

            String localPath = data;
            if (!String.IsNullOrEmpty(localPath))
            {
                String fullPath = Path.GetFullPath(localPath);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    //if localPath is not a file or folder on disk
                    throw new FileNotFoundException("Can't find artifact on disk.", localPath);
                }

                FileContainerHelper fileContainerHelper = new FileContainerHelper(projectUrl, projectCredential, projectId, containerId.Value, containerFolder);
                Task upload = fileContainerHelper.CopyToContainerAsync(context, localPath, context.CancellationToken);

                //else
                //{
                //    if (containerId > 0)
                //    {
                //        FileContainerHelper fileContainerHelper = new FileContainerHelper();
                //        fileContainerHelper.CreateContainerItem(jobEndpoint, projectId, containerId, containerFolder, cancellationToken);

                //        EventHandler<String> messageHandler = delegate (Object sender, String e)
                //        {
                //            result.LogMessage(e);
                //        };
                //        fileContainerHelper.ReportMessage += messageHandler;

                //        try
                //        {
                //            int count = fileContainerHelper.CopyToContainerAsync(jobEndpoint, localPath, projectId, containerId, containerFolder, cancellationToken).Result;
                //            result.LogMessage(LocalizationHelper.FormatString("Uploaded artifact '{0}' to container folder '{1}' of build {2}.", localPath, containerFolder, buildId));
                //        }
                //        finally
                //        {
                //            fileContainerHelper.ReportMessage -= messageHandler;
                //        }

                //        if (!String.IsNullOrEmpty(artifactName))
                //        {
                //            BuildHelper buildHelper = new BuildHelper();
                //            String fileContainerPath = String.Format("#/{0}/{1}", containerId, containerFolder);
                //            var artifact = buildHelper.AssociateArtifact(jobEndpoint, projectId, buildId, artifactName, WellKnownArtifactResourceTypes.Container, fileContainerPath, propertyDictionary, cancellationToken);
                //            result.LogMessage(LocalizationHelper.FormatString("Associated artifact {0} with build {1}", artifact.Id, buildId));
                //        }
                //    }
                //    else
                //    {
                //        result.LogError(LocalizationHelper.FormatString("Unable to find container for build: {0}", buildId));
                //        result.TaskResult = false;
                //    }
                //}
            }
            else
            {
                throw new Exception("Cannot upload artifact, artifact location is not specified.");
            }
        }

        private Boolean IsContainerPath(String path)
        {
            return !String.IsNullOrEmpty(path) && 
                    path.StartsWith("#", StringComparison.OrdinalIgnoreCase);
        }

        private Boolean IsValidServerPath(String path)
        {
            return !String.IsNullOrEmpty(path) &&
                    path.Length >= 2 && 
                    path[0] == '$' && 
                    (path[1] == '/' || path[1] == '\\');
        }

        private Boolean IsUncSharePath(IExecutionContext context, String path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }

            Uri uri;
            // Add try catch to avoid unexpected throw from Uri.Property.
            try
            {
                if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out uri))
                {
                    if (uri.IsAbsoluteUri && uri.IsUnc)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                context.Warning($"Can't determine path: {path} is UNC or not.");
                context.Debug(ex.ToString());
                return false;
            }

            return false;
        }

        private String InferArtifactResourceType(IExecutionContext context, String artifactLocation)
        {
            String type = "";
            if (!String.IsNullOrEmpty(artifactLocation))
            {
                // Prioritize UNC first as leading double-backslash can also match Tfvc VC paths (multiple slashes in a row are ignored)
                if (IsUncSharePath(context, artifactLocation))
                {
                    type = WellKnownArtifactResourceTypes.FilePath;
                }
                else if (IsValidServerPath(artifactLocation))
                {
                    // TFVC artifact
                    type = WellKnownArtifactResourceTypes.VersionControl;
                }
                else if (IsContainerPath(artifactLocation))
                {
                    // file container artifact
                    type = WellKnownArtifactResourceTypes.Container;
                }
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new Exception($"Can't infer artifact type from artifact location {artifactLocation}.");
            }

            return type;
        }

        private Dictionary<string, string> ExtractArtifactProperties(Dictionary<string, string> eventProperties)
        {
            return eventProperties.Where(pair =>
            !(String.Compare(pair.Key, ArtifactUploadEventProperties.ContainerFolder, StringComparison.OrdinalIgnoreCase) == 0
            || String.Compare(pair.Key, ArtifactUploadEventProperties.ArtifactName, StringComparison.OrdinalIgnoreCase) == 0
            || String.Compare(pair.Key, ArtifactAssociateEventProperties.ArtifactType, StringComparison.OrdinalIgnoreCase) == 0))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    internal class WellKnownArtifactCommand
    {
        public static readonly String Associate = "associate";
        public static readonly String Upload = "upload";
    }

    internal class ArtifactAssociateEventProperties
    {
        public static readonly String ArtifactName = "artifactname";
        public static readonly String ArtifactType = "artifacttype";
        public static readonly String Browsable = "Browsable";
    }

    public class ArtifactUploadEventProperties
    {
        public static readonly String ContainerFolder = "containerfolder";
        public static readonly String ArtifactName = "artifactname";
        public static readonly String Browsable = "Browsable";
    }

    public class ArtifactTaskName
    {
        public static readonly String AssociateArtifact = "AssociateArtifact";
        public static readonly String UploadArtifact = "UploadArtifact";
    }
}