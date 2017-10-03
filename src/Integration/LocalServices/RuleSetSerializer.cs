/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.CodeAnalysis.RuleSets;

namespace SonarLint.VisualStudio.Integration
{
    internal sealed class RuleSetSerializer : IRuleSetSerializer
    {
        private readonly IServiceProvider serviceProvider;

        public RuleSetSerializer(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            this.serviceProvider = serviceProvider;
        }

        public RuleSet LoadRuleSet(string ruleSetPath)
        {
            if (string.IsNullOrWhiteSpace(ruleSetPath))
            {
                throw new ArgumentNullException(nameof(ruleSetPath));
            }

            var fileSystem = this.serviceProvider.GetService<IFileSystem>();
            fileSystem.AssertLocalServiceIsNotNull();

            if (fileSystem.FileExist(ruleSetPath))
            {
                try
                {
                    return RuleSet.LoadFromFile(ruleSetPath);
                }
                catch (Exception ex) when (ex is InvalidRuleSetException || ex is XmlException || ex is IOException)
                {
                    // Log this for testing purposes
                    Trace.WriteLine(ex.ToString(), nameof(LoadRuleSet));
                }
            }

            return null;

        }

        public void WriteRuleSetFile(RuleSet ruleSet, string ruleSetPath)
        {
            if (ruleSet == null)
            {
                throw new ArgumentNullException(nameof(ruleSet));
            }

            if (string.IsNullOrWhiteSpace(ruleSetPath))
            {
                throw new ArgumentNullException(nameof(ruleSetPath));
            }

            // We want to prevent writing out the file if the contents has not changed to prevent TFS, 
            // or other source control, from marking the files as changed.
            if (!File.Exists(ruleSetPath))
            {
                ruleSet.WriteToFile(ruleSetPath);
            }
            else
            {
                var tempFile = Path.GetTempFileName();
                ruleSet.WriteToFile(tempFile);
                var newContent = File.ReadAllText(tempFile);
                var currentContent = File.ReadAllText(ruleSetPath);
                if (currentContent != newContent)
                {
                    File.WriteAllText(ruleSetPath, newContent);
                }
                File.Delete(tempFile);
            }
        }
    }
}
