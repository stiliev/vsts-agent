using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    [ServiceLocator(Default = typeof(TaskRunner))]
    public interface ITaskRunner : IStep, IAgentService
    {
        TaskInstance TaskInstance { get; set; }
    }

    public sealed class TaskRunner : AgentService, ITaskRunner
    {
        public bool AlwaysRun => TaskInstance?.AlwaysRun ?? default(bool);
        public bool ContinueOnError => TaskInstance?.ContinueOnError ?? default(bool);
        public bool Critical => false;
        public string DisplayName => TaskInstance?.DisplayName;
        public bool Enabled => TaskInstance?.Enabled ?? default(bool);
        public IExecutionContext ExecutionContext { get; set; }
        public bool Finally => false;
        public TaskInstance TaskInstance { get; set; }

        public async Task RunAsync()
        {
            // Validate args.
            Trace.Entering();
            ArgUtil.NotNull(ExecutionContext, nameof(ExecutionContext));
            ArgUtil.NotNull(ExecutionContext.Variables, nameof(ExecutionContext.Variables));
            ArgUtil.NotNull(TaskInstance, nameof(TaskInstance));
            var taskManager = HostContext.GetService<ITaskManager>();
            var handlerFactory = HostContext.GetService<IHandlerFactory>();

            // Load the task definition and choose the handler.
            // TODO: Add a try catch here to give a better error message.
            Definition definition = taskManager.Load(TaskInstance);
            ArgUtil.NotNull(definition, nameof(definition));
            HandlerData handlerData =
                definition.Data?.Execution?.All
                .OrderBy(x => !x.PreferredOnCurrentPlatform()) // Sort true to false.
                .ThenBy(x => x.Priority)
                .FirstOrDefault();
            if (handlerData == null)
            {
                // TODO: BETTER ERROR AND LOC
                throw new Exception("Supported handler not found.");
            }

            // Load the default input values from the definition.
            Trace.Verbose("Loading default inputs.");
            var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in (definition.Data?.Inputs ?? new TaskInputDefinition[0]))
            {
                string key = input?.Name?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    inputs[key] = input.DefaultValue?.Trim() ?? string.Empty;
                }
            }

            // Merge the instance inputs.
            Trace.Verbose("Loading instance inputs.");
            foreach (var input in (TaskInstance.Inputs as IEnumerable<KeyValuePair<string, string>> ?? new KeyValuePair<string, string>[0]))
            {
                string key = input.Key?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    inputs[key] = input.Value?.Trim() ?? string.Empty;
                }
            }

            // Expand the inputs.
            Trace.Verbose("Expanding inputs.");
            ExecutionContext.Variables.ExpandValues(target: inputs);

            // TODO: Delegate to the source provider to fixup the file path inputs.

            // Create the handler.
            IHandler handler = handlerFactory.Create(
                ExecutionContext,
                handlerData,
                inputs,
                taskDirectory: definition.Directory);

            List<Exception> allExs = new List<Exception>();
            try
            {
                // Run the task.
                await handler.RunAsync();
            }
            catch (Exception ex)
            {
                allExs.Add(ex);
            }

            // TODO: Merge result from ##vso command task queue.
            var context = ExecutionContext as ExecutionContext;
            ArgUtil.NotNull(context, nameof(context));

            // we will let all async commands run to finish.
            foreach (var command in context.AsyncCommands)
            {
                TaskResult asyncCommandResult = TaskResult.Succeeded;
                try
                {
                    // wait async command to finish.
                    await command.RunToFinish();
                }
                catch (Exception ex)
                {
                    asyncCommandResult = TaskResult.Failed;
                    allExs.Add(ex);
                }

                // fail the step if any async command failed.
                if (asyncCommandResult == TaskResult.Failed)
                {
                    ExecutionContext.Result = asyncCommandResult;
                }
            }

            // deal all exceptions;
            if (allExs.Count > 0)
            {
                throw new AggregateException(allExs).Flatten();
            }
        }
    }
}
