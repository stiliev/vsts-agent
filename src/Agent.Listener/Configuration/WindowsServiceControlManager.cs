using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    public class WindowsServiceControlManager : ServiceControlManager
    {
        private const string WindowsServiceControllerName = "Agent.Service.exe";
        private const string WindowsLogonAccount = "windowslogonaccount";
        private const string WindowsLogonPassword = "windowslogonpassword";
        private const string ServiceNamePattern = "vstsagent.{0}.{1}";
        private const string ServiceDisplayNamePattern = "VSTS Agent ({0}.{1})";

        public override bool ConfigureService(AgentSettings settings, Dictionary<string, string> args, bool enforceSupplied)
        {
            Trace.Info(nameof(ConfigureService));
            // TODO: Fix bug that exists in the legacy Windows agent where configuration using mirrored credentials causes an error, but the agent is still functional (after restarting). Mirrored credentials is a supported scenario and shouldn't manifest any errors.

            var consoleWizard = HostContext.GetService<IConsoleWizard>();
            string logonAccount = string.Empty;
            string logonPassword = string.Empty;
            NTAccount defaultServiceAccount = GetDefaultServiceAccount();

            logonAccount = consoleWizard.ReadValue(WindowsLogonAccount,
                                                StringUtil.Loc("WindowsLogonAccountNameDescription"),
                                                false,
                                                defaultServiceAccount.ToString(),
                                                Validators.NTAccountValidator,
                                                args,
                                                enforceSupplied);
            Trace.Info("LogonAccount: {0}", logonAccount);

            if (!defaultServiceAccount.Equals(new NTAccount(logonAccount)))
            {
                Trace.Verbose("Acquiring logon account password");
                logonPassword = consoleWizard.ReadValue(WindowsLogonPassword,
                                                    StringUtil.Loc("WindowsLogonPasswordDescription", logonAccount),
                                                    true,
                                                    string.Empty,
                                                    //TODO find how to validate using NativeMethods.LogonUser
                                                    Validators.NonEmptyValidator,
                                                    args,
                                                    enforceSupplied);

                Trace.Verbose("Acquired credential for logon account");
            }

            // TODO: Create a unique group VSTS_G{#HASH} and add the account to the group and grant permission for the group on the root folder
            CalculateServiceName(settings, ServiceNamePattern, ServiceDisplayNamePattern);

            string agentServiceExecutable = Path.Combine(IOUtil.GetBinPath(), WindowsServiceControllerName);

            // TODO check and unconfigure/configure the service using advapi32.dll service install methods using pinvoke
            return true;
        }

        public override void StartService(string serviceName)
        {
            throw new NotImplementedException();
        }

        public bool Unconfigure(string serviceName)
        {
            return true;
        }

        private static NTAccount GetDefaultServiceAccount()
        {
            SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, domainSid: null);
            NTAccount account = sid.Translate(typeof(NTAccount)) as NTAccount;

            if (account == null)
            {
                throw new InvalidOperationException(StringUtil.Loc("LocalServiceNotFound"));
            }

            // TODO: If its domain joined machine use WellKnownSidType.NetworkServiceSid?
            return account;
        }
    }
}
