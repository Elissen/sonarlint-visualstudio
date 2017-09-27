﻿/*
 * SonarQube Client
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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SonarQube.Client.Messages;
using SonarQube.Client.Models;

namespace SonarQube.Client.Services
{
    [Export(typeof(ISonarQubeService))]
    public class SonarQubeService : ISonarQubeService
    {
        internal const int MaximumPageSize = 500;
        internal readonly Version OrganizationsFeatureMinimalVersion = new Version(6, 2);

        private readonly ISonarQubeClient sonarqubeClient;

        private ConnectionRequest connection;
        private bool isConnected;
        private Version serverVersion;

        public SonarQubeService(ISonarQubeClient sonarqubeClient)
        {
            if (sonarqubeClient == null)
            {
                throw new ArgumentNullException(nameof(sonarqubeClient));
            }

            this.sonarqubeClient = sonarqubeClient;
        }

        public bool HasOrganizationsFeature
        {
            get
            {
                EnsureIsConnected();

                return this.serverVersion >= OrganizationsFeatureMinimalVersion;
            }
        }

        public async Task ConnectAsync(ConnectionInformation connection, CancellationToken token)
        {
            if (this.isConnected)
            {
                throw new InvalidOperationException("This operation expects the service not to be connected.");
            }

            var ConnectionRequest = new ConnectionRequest
            {
                Authentication = connection.Authentication,
                ServerUri = connection.ServerUri,
                Login = connection.UserName,
                Password = connection.Password
            };

            var credentialsValidationResult = await this.sonarqubeClient.ValidateCredentialsAsync(ConnectionRequest, token);
            credentialsValidationResult.EnsureSuccess();

            if (!credentialsValidationResult.Value.IsValid)
            {
                throw new Exception("Invalid credentials."); // TODO: Provide better exception
            }

            var versionResult = await this.sonarqubeClient.GetVersionAsync(ConnectionRequest, token);
            versionResult.EnsureSuccess();

            this.serverVersion = Version.Parse(versionResult.Value.Version);
            this.connection = ConnectionRequest;
            this.isConnected = true;
        }

        public async Task<IList<SonarQubeOrganization>> GetAllOrganizationsAsync(CancellationToken token)
        {
            EnsureIsConnected();

            int currentPage = 1;
            var allOrganizations = new List<OrganizationResponse>();
            Result<OrganizationResponse[]> organizationsResult;

            do
            {
                var request = new OrganizationRequest { Page = currentPage, PageSize = MaximumPageSize };
                organizationsResult = await this.sonarqubeClient.GetOrganizationsAsync(this.connection, request, token);
                organizationsResult.EnsureSuccess();

                allOrganizations.AddRange(organizationsResult.Value);

                currentPage++;
            }
            while (organizationsResult.Value.Length > 0);

            return allOrganizations.Select(SonarQubeOrganization.FromResponse).ToList();
        }

        public async Task<IList<SonarQubePlugin>> GetAllPluginsAsync(CancellationToken token)
        {
            EnsureIsConnected();

            var pluginsResult = await this.sonarqubeClient.GetPluginsAsync(this.connection, token);
            pluginsResult.EnsureSuccess();

            return pluginsResult.Value.Select(SonarQubePlugin.FromResponse).ToList();
        }

        public async Task<IList<SonarQubeProject>> GetAllProjectsAsync(string organizationKey, CancellationToken token)
        {
            EnsureIsConnected();

            if (organizationKey == null) // Orgs are not supported or not enabled
            {
                var projectsResult = await this.sonarqubeClient.GetProjectsAsync(this.connection, token);
                projectsResult.EnsureSuccess();

                return projectsResult.Value.Select(SonarQubeProject.FromResponse).ToList();
            }

            int currentPage = 1;
            var allProjects = new List<ComponentResponse>();
            Result<ComponentResponse[]> componentsResult;
            do
            {
                var request = new ComponentRequest { Page = currentPage, PageSize = MaximumPageSize };
                componentsResult = await this.sonarqubeClient.GetComponentsSearchProjectsAsync(this.connection,
                    request, token);
                componentsResult.EnsureSuccess();

                allProjects.AddRange(componentsResult.Value);
                currentPage++;
            }
            while (componentsResult.Value.Length > 0);

            return allProjects.Select(SonarQubeProject.FromResponse).ToList();
        }

        public async Task<IList<SonarQubeProperty>> GetAllPropertiesAsync(CancellationToken token)
        {
            EnsureIsConnected();

            var propertiesResult = await this.sonarqubeClient.GetPropertiesAsync(this.connection, token);
            propertiesResult.EnsureSuccess();

            return propertiesResult.Value.Select(SonarQubeProperty.FromResponse).ToList();
        }

        public Uri GetProjectDashboardUrl(string projectKey)
        {
            EnsureIsConnected();

            const string ProjectDashboardRelativeUrl = "dashboard/index/{0}";

            return new Uri(this.connection.ServerUri, string.Format(ProjectDashboardRelativeUrl, projectKey));
        }

        public async Task<SonarQubeQualityProfile> GetQualityProfileAsync(string projectKey, SonarQubeLanguage language,
                    CancellationToken token)
        {
            EnsureIsConnected();

            var qualityProfileRequest = new QualityProfileRequest { ProjectKey = projectKey };
            var qualityProfilesResult = await this.sonarqubeClient.GetQualityProfilesAsync(this.connection, qualityProfileRequest,
                token);

            // Special handling for the case when a project was not analyzed yet, in which case a 404 is returned
            if (qualityProfilesResult.StatusCode == HttpStatusCode.NotFound)
            {
                qualityProfileRequest = new QualityProfileRequest { ProjectKey = null };
                qualityProfilesResult = await this.sonarqubeClient.GetQualityProfilesAsync(this.connection, qualityProfileRequest,
                    token);
            }

            qualityProfilesResult.EnsureSuccess();

            var profilesWithGivenLanguage = qualityProfilesResult.Value.Where(x => x.Language == language.Key).ToList();

            var qualityProfile = profilesWithGivenLanguage.Count > 1
                ? profilesWithGivenLanguage.Single(x => x.IsDefault)
                : profilesWithGivenLanguage.Single();

            var qualityProfileChangeLogRequest = new QualityProfileChangeLogRequest
            {
                PageSize = 1,
                QualityProfileKey = qualityProfile.Key
            };
            var changeLog = await this.sonarqubeClient.GetQualityProfileChangeLogAsync(this.connection,
                qualityProfileChangeLogRequest, token);
            changeLog.EnsureSuccess();

            return SonarQubeQualityProfile.FromResponse(qualityProfile, changeLog.Value.Events.Single().Date);
        }

        public async Task<RoslynExportProfileResponse> GetRoslynExportProfileAsync(string qualityProfileName,
            SonarQubeLanguage language, CancellationToken token)
        {
            EnsureIsConnected();

            var request = new RoslynExportProfileRequest { QualityProfileName = qualityProfileName, LanguageKey = language.Key };
            var roslynExportResult = await this.sonarqubeClient.GetRoslynExportProfileAsync(this.connection, request, token);
            roslynExportResult.EnsureSuccess();

            return roslynExportResult.Value;
        }

        public async Task<IList<SonarQubeIssue>> GetSuppressedIssuesAsync(string key, CancellationToken token)
        {
            EnsureIsConnected();

            var allIssuesResult = await this.sonarqubeClient.GetIssuesAsync(this.connection, key, token);
            allIssuesResult.EnsureSuccess();

            return allIssuesResult.Value
                .Where(x =>
                {
                    var resolutionState = SonarQubeIssue.ParseResolutionState(x.Resolution);
                    return resolutionState == SonarQubeIssueResolutionState.WontFix ||
                        resolutionState == SonarQubeIssueResolutionState.FalsePositive;
                })
                .Select(SonarQubeIssue.FromResponse)
                .ToList();
        }

        public async Task<IList<SonarQubeNotification>> GetNotificationEventsAsync(string projectKey,
                DateTimeOffset eventsSince, CancellationToken token)
        {
            EnsureIsConnected();

            var request = new NotificationsRequest { ProjectKey = projectKey, EventsSince = eventsSince };
            Result<NotificationsResponse[]> eventsResult = await this.sonarqubeClient.GetNotificationEventsAsync(this.connection,
                request, token);

            if (!eventsResult.IsSuccess)
            {
                return eventsResult.StatusCode == HttpStatusCode.NotFound
                    ? null : new List<SonarQubeNotification>();
            }

            return eventsResult.Value.Select(SonarQubeNotification.FromResponse).ToList();
        }



        internal void EnsureIsConnected()
        {
            if (!this.isConnected)
            {
                throw new InvalidOperationException("This operation expects the service to be connected.");
            }
        }
    }
}