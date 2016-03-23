using Build2 = Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public class BuildHelper
    {
        private static readonly int _httpRetryCount = 3;

        public async Task<Build2.BuildArtifact> AssociateArtifact(
            Uri projectCollection,
            VssCredentials credentials,
            Guid projectId,
            Int32 buildId,
            String name,
            String type,
            String data,
            Dictionary<String, String> propertiesDictionary,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Build2.BuildArtifact artifact = new Build2.BuildArtifact()
            {
                Name = name,
                Resource = new Build2.ArtifactResource()
                {
                    Data = data,
                    Type = type,
                    Properties = propertiesDictionary
                }
            };

            Build2.BuildHttpClient buildHttpClient = new Build2.BuildHttpClient(projectCollection, credentials, new VssHttpRetryMessageHandler(_httpRetryCount));
            return await buildHttpClient.CreateArtifactAsync(artifact, projectId, buildId, cancellationToken);
        }

        public async Task<Build2.Build> UpdateBuildNumber(
            Uri projectCollection,
            VssCredentials credentials,
            Guid projectId,
            Int32 buildId,
            String buildNumber,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Build2.Build build = new Build2.Build()
            {
                Id = buildId,
                BuildNumber = buildNumber,
                Project = new TeamProjectReference()
                {
                    Id = projectId,
                },
            };

            Build2.BuildHttpClient buildHttpClient = new Build2.BuildHttpClient(projectCollection, credentials, new VssHttpRetryMessageHandler(_httpRetryCount));
            return await buildHttpClient.UpdateBuildAsync(build, projectId, buildId, cancellationToken);
        }

        public async Task<IEnumerable<String>> AddBuildTag(
            Uri projectCollection,
            VssCredentials credentials,
            Guid projectId,
            Int32 buildId,
            String buildTag,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Build2.BuildHttpClient buildHttpClient = new Build2.BuildHttpClient(projectCollection, credentials, new VssHttpRetryMessageHandler(_httpRetryCount));
            return await buildHttpClient.AddBuildTagAsync(projectId, buildId, buildTag, cancellationToken: cancellationToken);
        }
    }
}
