using System;

namespace Microsoft.VisualStudio.Services.Agent
{
    public static class Constants
    {
        public static class Agent
        {
            public static readonly int MaxParallelism = 1;
            public static readonly string Version = "1.999.0";
        }

        public static class Build
        {
            public static class Path
            {
                public static readonly string ArtifactsDirectory = "a";
                public static readonly string BinariesDirectory = "b";
                public static readonly string GarbageCollectionDirectory = "GC";
                public static readonly string LegacyArtifactsDirectory = "artifacts";
                public static readonly string LegacyStagingDirectory = "staging";
                public static readonly string SourceRootMappingDirectory = "SourceRootMapping";
                public static readonly string SourcesDirectory = "s";
                public static readonly string TestResultsDirectory = "TestResults";
                public static readonly string TopLevelTrackingConfigFile = "Mappings.json";
                public static readonly string TrackingConfigFile = "SourceFolder.json";
            }
        }

        public static class Path
        {
            public static readonly string DiagDirectory = "_diag";
            public static readonly string ExternalsDirectory = "externals";
            public static readonly string TaskJsonFile = "task.json";
            public static readonly string TasksDirectory = "_tasks";
        }

        public static class Variables
        {
            public static readonly string MacroPrefix = "$(";
            public static readonly string MacroSuffix = ")";

            public static class Agent
            {
                //
                // Keep alphabetical
                //
                public static readonly string BuildFolder = "agent.builddirectory";
                public static readonly string HomeDirectory = "agent.homedirectory";
                public static readonly string Id = "agent.id";
                public static readonly string JobName = "agent.jobname";
                public static readonly string JobStatus = "agent.jobstatus";
                public static readonly string MachineName = "agent.machinename";
                public static readonly string Name = "agent.name";
                public static readonly string OS = "agent.os";
                public static readonly string OSVersion = "agent.osversion";
                public static readonly string RootFolder = "agent.RootDirectory";
                public static readonly string ServerOMFolder = "agent.ServerOMDirectory";
                public static readonly string WorkFolder = "agent.workfolder";
                public static readonly string WorkingFolder = "agent.WorkingDirectory";
            }

            public static class Build
            {
                //
                // Keep alphabetical
                //
                public static readonly string ArtifactStagingFolder = "build.artifactstagingdirectory";
                public static readonly string BinariesFolder = "build.binariesdirectory";
                public static readonly string Clean = "build.clean";
                public static readonly string DefinitionName = "build.definitionname";
                public static readonly string RepoClean = "build.repository.clean";
                public static readonly string RepoGitSubmoduleCheckout = "build.repository.git.submodulecheckout";
                public static readonly string RepoId = "build.repository.id";
                public static readonly string RepoLocalPath = "build.repository.localpath";
                public static readonly string RepoName = "build.Repository.name";
                public static readonly string RepoProvider = "build.repository.provider";
                public static readonly string RepoUri = "build.repository.uri";
                public static readonly string SourceBranch = "build.sourcebranch";
                public static readonly string SourceVersion = "build.sourceversion";
                public static readonly string SourceFolder = "build.sourcesdirectory";
                public static readonly string StagingFolder = "build.stagingdirectory";
                public static readonly string SyncSources = "build.syncSources";
            }

            public static class Common
            {
                public static readonly string TestResultsDirectory = "common.testresultsdirectory";
            }

            public static class System
            {
                //
                // Keep alphabetical
                //
                public static readonly string AccessToken = "system.accessToken";
                public static readonly string ArtifactsDirectory = "system.artifactsdirectory";
                public static readonly string CollectionId = "system.collectionid";
                public static readonly string Culture = "system.culture";
                public static readonly string Debug = "system.debug";
                public static readonly string DefaultWorkingDirectory = "system.defaultworkingdirectory";
                public static readonly string DefinitionId = "system.definitionid";
                public static readonly string EnableAccessToken = "system.enableAccessToken";
                public static readonly string HostType = "system.hosttype";
                // public static readonly string System = "system";
                public static readonly string TeamProject = "system.teamproject";
                // back compat variable, do not document
                public static readonly string TFServerUrl = "system.TeamFoundationServerUri";
                public static readonly string PreferGit = "system.prefergit";
            }
        }
    }
}