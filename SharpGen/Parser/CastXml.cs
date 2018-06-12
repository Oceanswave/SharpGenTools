// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using Microsoft.Win32;
using SharpGen.Config;
using SharpGen.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpGen.Parser
{
    /// <summary>
    /// CastXML front end for command line.
    /// see https://github.com/CastXML/CastXML
    /// </summary>
    public class CastXml
    {
        private static readonly Regex MatchError = new Regex("error:");

        /// <summary>
        /// GccXml tag for FundamentalType
        /// </summary>
        public const string TagFundamentalType = "FundamentalType";

        /// <summary>
        /// GccXml tag for Enumeration
        /// </summary>
        public const string TagEnumeration = "Enumeration";

        /// <summary>
        /// GccXml tag for Struct
        /// </summary>
        public const string TagStruct = "Struct";

        /// <summary>
        /// GccXml tag for Field
        /// </summary>
        public const string TagField = "Field";

        /// <summary>
        /// GccXml tag for Union
        /// </summary>
        public const string TagUnion = "Union";

        /// <summary>
        /// GccXml tag for Typedef
        /// </summary>
        public const string TagTypedef = "Typedef";

        /// <summary>
        /// GccXml tag for Function
        /// </summary>
        public const string TagFunction = "Function";

        /// <summary>
        /// GccXml tag for PointerType
        /// </summary>
        public const string TagPointerType = "PointerType";

        /// <summary>
        /// GccXml tag for ArrayType
        /// </summary>
        public const string TagArrayType = "ArrayType";

        /// <summary>
        /// GccXml tag for ReferenceType
        /// </summary>
        public const string TagReferenceType = "ReferenceType";

        /// <summary>
        /// GccXml tag for CvQualifiedType
        /// </summary>
        public const string TagCvQualifiedType = "CvQualifiedType";

        /// <summary>
        /// GccXml tag for Namespace
        /// </summary>
        public const string TagNamespace = "Namespace";

        /// <summary>
        /// GccXml tag for Variable
        /// </summary>
        public const string TagVariable = "Variable";

        /// <summary>
        /// GccXml tag for FunctionType
        /// </summary>
        public const string TagFunctionType = "FunctionType";

        /// <summary>
        /// Gets or sets the executable path of gccxml.exe.
        /// </summary>
        /// <value>The executable path.</value>
        public string ExecutablePath { get; }

        public string OutputPath { get; set; }

        private readonly IncludeDirectoryResolver directoryResolver;

        public Logger Logger { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CastXml"/> class.
        /// </summary>
        public CastXml(Logger logger, IncludeDirectoryResolver resolver, string executablePath)
        {
            Logger = logger;
            ExecutablePath = executablePath;
            directoryResolver = resolver;
        }

        /// <summary>
        /// Preprocesses the specified header file.
        /// </summary>
        /// <param name="headerFile">The header file.</param>
        /// <param name="handler">The handler.</param>
        public void Preprocess(string headerFile, DataReceivedEventHandler handler)
        {
            Logger.RunInContext(nameof(Preprocess), () =>
            {
                if (!File.Exists(ExecutablePath))
                    Logger.Fatal("castxml.exe not found from path: [{0}]", ExecutablePath);

                if (!File.Exists(headerFile))
                    Logger.Fatal("C++ Header file [{0}] not found", headerFile);

                RunCastXml(headerFile, handler, "-E -dD");
            });
        }

        /// <summary>
        /// Processes the specified header headerFile.
        /// </summary>
        /// <param name="headerFile">The header headerFile.</param>
        /// <returns></returns>
        public StreamReader Process(string headerFile)
        {
            StreamReader result = null;

            Logger.RunInContext(nameof(Process), () =>
            {
                if (!File.Exists(ExecutablePath)) Logger.Fatal("castxml.exe not found from path: [{0}]", ExecutablePath);

                if (!File.Exists(headerFile)) Logger.Fatal("C++ Header file [{0}] not found", headerFile);

                var xmlFile = Path.ChangeExtension(headerFile, "xml");

                // Delete any previously generated xml file
                File.Delete(xmlFile);

                RunCastXml(headerFile, LogCastXmlOutput, $"-o {xmlFile}");

                if (!File.Exists(xmlFile) || Logger.HasErrors)
                {
                    Logger.Error(LoggingCodes.CastXmlFailed, "Unable to generate XML file with castxml [{0}]. Check previous errors.", xmlFile);
                }
                else
                {
                    result = File.OpenText(xmlFile);
                }
            });

            return result;
        }

        private void RunCastXml(string headerFile, DataReceivedEventHandler outputDataCallback, string additionalArguments)
        {
            using (var currentProcess = new Process())
            {
                var startInfo = new ProcessStartInfo(ExecutablePath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = OutputPath
                };

                var arguments = GetCastXmlArgs();
                var builder = new System.Text.StringBuilder();
                builder.Append(arguments).Append(" ").Append(additionalArguments);

                foreach (var directory in directoryResolver.IncludePaths)
                {
                    builder.Append(" ").Append(directory);
                }
                arguments = builder.ToString();

                startInfo.Arguments = arguments + " " + $"\"{headerFile}\"";
                Logger.Message($"CastXML {builder}");
                currentProcess.StartInfo = startInfo;
                currentProcess.ErrorDataReceived += ProcessErrorFromHeaderFile;
                currentProcess.OutputDataReceived += outputDataCallback;
                currentProcess.Start();
                currentProcess.BeginOutputReadLine();
                currentProcess.BeginErrorReadLine();

                currentProcess.WaitForExit();

                if (Logger.HasErrors)
                {
                    Logger.Error(LoggingCodes.CastXmlFailed, "Failed to run CastXML. Check previous errors.");
                }
            }
        }

        private static string GetCastXmlArgs()
        {
            var arguments = "";
            arguments += " --castxml-gccxml";
            arguments += " -x c++ -std=c++11 -fmsc-version=1900 -fms-extensions -fms-compatibility";
            arguments += " -Wno-microsoft-enum-value -Wmacro-redefined -Wno-invalid-token-paste -Wno-ignored-attributes";
            return arguments;
        }

        // path/to/header.h:68:1: error:
        private static Regex matchFileErrorRegex = new Regex(@"^(.*):(\d+):(\d+):\s+error:(.*)");

        /// <summary>
        /// Processes the error from header file.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Diagnostics.DataReceivedEventArgs"/> instance containing the event data.</param>
        void ProcessErrorFromHeaderFile(object sender, DataReceivedEventArgs e)
        {
            var popContext = false;
            try
            {
                if (e.Data != null)
                {
                    var matchError = matchFileErrorRegex.Match(e.Data);

                    var errorText = e.Data;

                    if (matchError.Success)
                    {
                        Logger.PushLocation(matchError.Groups[1].Value, int.Parse(matchError.Groups[2].Value), int.Parse(matchError.Groups[3].Value));
                        popContext = true;
                        errorText = matchError.Groups[4].Value;
                    }

                    if (MatchError.Match(e.Data).Success)
                        Logger.Error(LoggingCodes.CastXmlError, errorText);
                    else
                        Logger.Warning(LoggingCodes.CastXmlWarning, errorText);
                }
            }
            finally
            {
                if (popContext)
                    Logger.PopLocation();
            }
        }

        /// <summary>
        /// Processes the output from header file.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Diagnostics.DataReceivedEventArgs"/> instance containing the event data.</param>
        void LogCastXmlOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                Logger.Message(e.Data);
        }
    }
}