﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PowerShellClient
{
    public class PowerShellCommand
    {
        private readonly string _script;
        private readonly List<PowerShellParameter> _parameters;

        public PowerShellCommand(string script)
        {
            ThrowIfNull(script, nameof(script));

            _script = script;
            _parameters = new List<PowerShellParameter>();
        }

        public int CommandTimeoutSeconds { get; set; } = 30;

        public void AddArgument(object value, ParameterQuoteOptions quoteOptions = ParameterQuoteOptions.Quote)
        {
            ThrowIfNull(value, nameof(value));

            _parameters.Add(new PowerShellParameter(value, quoteOptions));
        }

        public void AddParameter(object value, string name, ParameterQuoteOptions quoteOptions = ParameterQuoteOptions.Quote)
        {
            ThrowIfNull(value, nameof(value));
            ThrowIfNull(name, nameof(name));

            _parameters.Add(new PowerShellNamedParameter(name, value, quoteOptions));
        }

        public string ExecuteScalar()
        {
            return Execute(GetCommandString()).ReadToEnd().Trim();
        }

        public PowerShellDataReader ExecuteDataReader()
        {
            return new PowerShellDataReader(Execute($"{GetCommandString()} | Format-List"));
        }

        public async Task ExecuteStreamAsync(DataReceivedEventHandler dataReceivedEventHandler)
        {
            var processStartInfo = GetProcessStartInfo();

            await Task.Run(() =>
            {
                using (var ps = new Process())
                {
                    ps.StartInfo = processStartInfo;

                    ps.OutputDataReceived += dataReceivedEventHandler;
                    ps.ErrorDataReceived += dataReceivedEventHandler;

                    ps.Start();

                    ps.BeginOutputReadLine();
                    ps.BeginErrorReadLine();

                    ps.WaitForExit();
                }
            });
        }

        private StreamReader Execute(string command)
        {
            var processStartInfo = GetProcessStartInfo();

            using (var ps = new Process())
            {
                ps.StartInfo = processStartInfo;

                ps.Start();
                ps.WaitForExit(CommandTimeoutSeconds * 1000);

                if (ps.ExitCode != 0)
                {
                    var errorMessage = ps.StandardError.ReadToEnd();
                    throw new PowerShellException(errorMessage);
                }

                var errorStream = ps.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(errorStream))
                {
                    throw new PowerShellException(errorStream);
                }

                return ps.StandardOutput;
            }
        }

        private ProcessStartInfo GetProcessStartInfo()
        {
            return new ProcessStartInfo()
            {
                FileName = "powershell",
                Arguments = GetCommandString(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
        }

        private string GetCommandString()
        {
            var builder = new StringBuilder(_script);

            foreach (var param in _parameters)
            {
                builder.Append($" {param.ToString()} ");
            }

            return builder.ToString();
        }

        private static void ThrowIfNull(object value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
