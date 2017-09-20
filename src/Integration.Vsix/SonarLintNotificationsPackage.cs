﻿/*
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SonarLint.VisualStudio.Integration.Notifications;

namespace SonarLint.VisualStudio.Integration.Vsix
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad(UIContextGuids.NoSolution)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists)]
    public sealed class SonarLintNotificationsPackage : Package
    {
        /// <summary>
        /// SonarLintNotifications GUID string.
        /// </summary>
        public const string PackageGuidString = "c26b6802-dd9c-4a49-b8a5-0ad8ef04c579";

        private const string NotificationDataKey = "NotificationEventData";
        private IActiveSolutionBoundTracker activeSolutionBoundTracker;
        private ISonarQubeNotifications notifications;
        private readonly IFormatter formatter = new BinaryFormatter();
        private NotificationData notificationData;

        private NotificationIndicator notificationIcon;

        protected override void Initialize()
        {
            AddOptionKey(NotificationDataKey);
            base.Initialize();

            notifications = this.GetMefService<ISonarQubeNotifications>();

            activeSolutionBoundTracker = this.GetMefService<IActiveSolutionBoundTracker>();
            activeSolutionBoundTracker.SolutionBindingChanged += OnSolutionBindingChanged;
        }

        private void OnSolutionBindingChanged(object sender, bool isBound)
        {
            if (notificationIcon == null)
            {
                notificationIcon = new NotificationIndicator();
                notificationIcon.DataContext = notifications;
                VisualStudioStatusBarHelper.AddStatusBarIcon(notificationIcon);
            }

            if (isBound)
            {
                notifications.Start(notificationData);
            }
            else
            {
                notifications.Stop();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (notificationIcon != null)
            {
                VisualStudioStatusBarHelper.RemoveStatusBarIcon(notificationIcon);
            }

            activeSolutionBoundTracker.SolutionBindingChanged -= OnSolutionBindingChanged;

            notifications.Dispose();
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            if (key == NotificationDataKey)
            {
                formatter.Serialize(stream, notifications.NotificationData);
            }
        }

        protected override void OnLoadOptions(string key, Stream stream)
        {
            if (key == NotificationDataKey)
            {
                try
                {
                    notificationData = formatter.Deserialize(stream) as NotificationData;
                }
                catch (Exception)
                {
                    // mbTODO: better idea than catching exception?
                    // mbTODO: trace?
                    notificationData = null;
                }
            }
        }

    }
}
