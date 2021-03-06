﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using DD.CBU.CaasDeploy.Library.Contracts;
using DD.CBU.CaasDeploy.Library.Models;
using Newtonsoft.Json.Linq;

namespace DD.CBU.CaasDeploy.Library.Utilities
{
    /// <summary>
    /// Helper class to deploy or delete a resource.
    /// </summary>
    internal class ResourceDeployer
    {
        /// <summary>
        /// The resource API URLs.
        /// </summary>
        private readonly static Dictionary<ResourceType, CaasApiUrls> ResourceApis = new Dictionary<ResourceType, CaasApiUrls>
        {
            { ResourceType.NetworkDomain, new CaasApiUrls { DeployUrl = "/network/deployNetworkDomain", GetUrl = "/network/networkDomain/{0}", ListUrl = "/network/networkDomain?name={0}", DeleteUrl = "/network/deleteNetworkDomain", EditUrl = "/network/editNetworkDomain" } },
            { ResourceType.Vlan, new CaasApiUrls { DeployUrl = "/network/deployVlan", GetUrl = "/network/vlan/{0}", ListUrl = "/network/vlan?networkDomainId={0}&name={1}", DeleteUrl = "/network/deleteVlan", EditUrl = "/network/editVlan" } },
            { ResourceType.Server, new CaasApiUrls { DeployUrl = "/server/deployServer", GetUrl = "/server/server/{0}", ListUrl = "/server/server?networkDomainId={0}&name={1}", DeleteUrl = "/server/deleteServer", EditUrl = null } },
            { ResourceType.FirewallRule, new CaasApiUrls { DeployUrl = "/network/createFirewallRule", GetUrl = "/network/firewallRule/{0}", ListUrl = "/network/firewallRule?networkDomainId={0}&name={1}", DeleteUrl = "/network/deleteFirewallRule", EditUrl = "/network/editFirewallRule" } },
            { ResourceType.PublicIpBlock, new CaasApiUrls { DeployUrl = "/network/addPublicIpBlock", GetUrl = "/network/publicIpBlock/{0}", ListUrl = null, DeleteUrl = "/network/removePublicIpBlock", EditUrl = null } },
            { ResourceType.NatRule, new CaasApiUrls { DeployUrl = "/network/createNatRule", GetUrl = "/network/natRule/{0}", ListUrl = "/network/natRule?networkDomainId={0}&internalIp={1}", DeleteUrl = "/network/deleteNatRule", EditUrl = null } },
            { ResourceType.VirtualListener, new CaasApiUrls { DeployUrl = "/networkDomainVip/createVirtualListener", GetUrl = "/networkDomainVip/virtualListener/{0}", ListUrl = "/networkDomainVip/virtualListener?networkDomainId={0}&name={1}", DeleteUrl = "/networkDomainVip/deleteVirtualListener", EditUrl = "/networkDomainVip/editVirtualListener" } },
            { ResourceType.Pool, new CaasApiUrls { DeployUrl = "/networkDomainVip/createPool", GetUrl = "/networkDomainVip/pool/{0}", ListUrl = "/networkDomainVip/pool?networkDomainId={0}&name={1}", DeleteUrl = "/networkDomainVip/deletePool", EditUrl = "/networkDomainVip/editPool" } },
            { ResourceType.Node, new CaasApiUrls { DeployUrl = "/networkDomainVip/createNode", GetUrl = "/networkDomainVip/node/{0}", ListUrl = "/networkDomainVip/node?networkDomainId={0}&name={1}", DeleteUrl = "/networkDomainVip/deleteNode",  EditUrl = "/networkDomainVip/editNode" } },
            { ResourceType.PoolMember, new CaasApiUrls { DeployUrl = "/networkDomainVip/addPoolMember", GetUrl = "/networkDomainVip/poolMember/{0}", ListUrl = "/networkDomainVip/poolMember?poolId={0}&nodeId={1}", DeleteUrl = "/networkDomainVip/removePoolMember", EditUrl = "/networkDomainVip/editPoolMember" } },
        };

        /// <summary>
        /// The properties not supported for edit.
        /// </summary>
        private readonly static Dictionary<ResourceType, string[]> PropertiesNotSupportedForEdit = new Dictionary<ResourceType, string[]>
        {
            { ResourceType.NetworkDomain, new[] { "datacenterId" } },
            { ResourceType.Vlan, new[] { "networkDomainId", "privateIpv4BaseAddress" } },
            { ResourceType.Server, new[] { "networkDomainId" } },
            { ResourceType.FirewallRule, new[] { "networkDomainId", "name" } },
            { ResourceType.PublicIpBlock, new[] { "networkDomainId" } },
            { ResourceType.NatRule, new[] { "networkDomainId" } },
            { ResourceType.VirtualListener, new[] { "networkDomainId" } },
            { ResourceType.Pool, new[] { "networkDomainId", "name" } },
            { ResourceType.Node, new[] { "networkDomainId" } },
            { ResourceType.PoolMember, new[] { "networkDomainId" } },
        };

        /// <summary>
        /// The MCP2 base API URL.
        /// </summary>
        private const string Mcp2UrlStem = "/caas/2.0";

        /// <summary>
        /// The polling delay in seconds.
        /// </summary>
        private const int PollingDelaySeconds = 30;

        /// <summary>
        /// The polling time out in minutes.
        /// </summary>
        private const int PollingTimeOutMinutes = 20;

        /// <summary>
        /// The log provider
        /// </summary>
        private readonly ILogProvider _logProvider;

        /// <summary>
        /// The account details
        /// </summary>
        private readonly CaasAccountDetails _accountDetails;

        /// <summary>
        /// The resource API URLs for the resource.
        /// </summary>
        private readonly CaasApiUrls _resourceApi;

        /// <summary>
        /// The resource identifier
        /// </summary>
        private readonly string _resourceId;

        /// <summary>
        /// The resource type
        /// </summary>
        private readonly ResourceType _resourceType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceDeployer"/> class.
        /// </summary>
        /// <param name="runtimeContext">The runtime context.</param>
        /// <param name="resourceId">The resource identifier.</param>
        /// <param name="resourceType">Type of the resource.</param>
        public ResourceDeployer(RuntimeContext runtimeContext, string resourceId, ResourceType resourceType)
        {
            _resourceId = resourceId;
            _resourceType = resourceType;
            _resourceApi = ResourceApis[resourceType];
            _accountDetails = runtimeContext.AccountDetails;
            _logProvider = runtimeContext.LogProvider;
        }

        /// <summary>
        /// Deploys the supplied resource and waits.
        /// </summary>
        /// <param name="resourceDefinition">The resource definition.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        public async Task<ResourceLog> DeployAndWait(JObject resourceDefinition)
        {
            var response = new ResourceLog() { ResourceId = _resourceId, ResourceType = _resourceType };

            try
            {
                if (_resourceApi.ListUrl != null)
                {
                    var ids = GetResourceIdentifiers(resourceDefinition);
                    var existingResourceDetails = (await GetResourceByIdentifiers(ids)).SingleOrDefault();
                    if (existingResourceDetails != null)
                    {
                        if (_resourceApi.EditUrl == null)
                        {
                            _logProvider.LogMessage($"Resource '{_resourceId}' already exists and cannot be updated. Using existing resource even if its definition doesn't match the template.");
                            response.Details = existingResourceDetails;
                            response.CaasId = response.Details["id"].Value<string>();
                            response.DeploymentStatus = ResourceLogStatus.UsedExisting;
                            return response;
                        }
                        else
                        {
                            var existingId = existingResourceDetails["id"].Value<string>();
                            await UpdateExistingResource(existingId, resourceDefinition);
                            response.Details = await Get(existingId);
                            response.CaasId = response.Details["id"].Value<string>();
                            response.DeploymentStatus = ResourceLogStatus.Updated;
                            return response;
                        }
                    }
                }

                var id = await DeployNewResource(resourceDefinition);
                response.Details = await WaitForDeploy(id);
                response.CaasId = response.Details["id"].Value<string>();
                response.DeploymentStatus = ResourceLogStatus.Deployed;
                return response;
            }
            catch (CaasException ex)
            {
                _logProvider.LogError(ex.Message);

                response.DeploymentStatus = ResourceLogStatus.Failed;
                response.Error = new Error
                {
                    Message = ex.Message,
                    Operation = ex.Operation,
                    ResponseCode = ex.ResponseCode
                };

                return response;
            }
        }

        /// <summary>
        /// Deploys the supplied resource.
        /// </summary>
        /// <param name="resourceDefinition">The resource definition.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        private async Task<string> DeployNewResource(JObject resourceDefinition)
        {
            _logProvider.LogMessage($"Deploying {_resourceType}: '{_resourceId}' ");

            using (var client = HttpClientFactory.GetClient(_accountDetails, "application/json"))
            {
                var url = GetApiUrl(_resourceApi.DeployUrl);
                var response = await client.PostAsync(url, new StringContent(resourceDefinition.ToString(), Encoding.UTF8, "application/json"));
                response.ThrowForHttpFailure();
                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);
                var info = (JArray)jsonResponse["info"];
                // TODO: Check if we ever get more than 1 
                return info[0]["value"].Value<string>();
            }
        }

        /// <summary>
        /// Updates an existing resource.
        /// </summary>
        /// <param name="existingId">The existing resource identifier.</param>
        /// <param name="resourceDefinition">The resource definition.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        private async Task UpdateExistingResource(string existingId, JObject resourceDefinition)
        {
            _logProvider.LogMessage($"Updating existing {_resourceType}: '{_resourceId}' ");

            using (var client = HttpClientFactory.GetClient(_accountDetails, "application/json"))
            {
                resourceDefinition.AddFirst(new JProperty("id", existingId));
                RemovePropertiesUnsupportedForEdit(resourceDefinition);
                var url = GetApiUrl(_resourceApi.EditUrl);
                var content = new StringContent(resourceDefinition.ToString(), Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); // CaaS bug is causing errors if charset is preset in the content-type header
                var response = await client.PostAsync(url, content);
                response.ThrowForHttpFailure();
                var responseBody = await response.Content.ReadAsStringAsync();
            }
        }

        /// <summary>
        /// Removes the properties unsupported for edit.
        /// </summary>
        /// <param name="resourceDefinition">The resource definition.</param>
        private void RemovePropertiesUnsupportedForEdit(JObject resourceDefinition)
        {
            foreach (var prop in PropertiesNotSupportedForEdit[_resourceType])
            {
                resourceDefinition.Remove(prop);
            }
        }

        /// <summary>
        /// Gets a resource by CaaS identifier.
        /// </summary>
        /// <param name="caasId">The CaaS identifier.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        public async Task<JObject> Get(string caasId)
        {
            using (var client = HttpClientFactory.GetClient(_accountDetails, "application/json"))
            {
                var url = String.Format(GetApiUrl(_resourceApi.GetUrl), caasId);
                var response = await client.GetAsync(url);
                response.ThrowForHttpFailure();
                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);
                return jsonResponse;
            }
        }

        /// <summary>
        /// Deletes a resource by CaaS identifier and waits.
        /// </summary>
        /// <param name="caasId">The CaaS identifier.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        public async Task<ResourceLog> DeleteAndWait(string caasId)
        {
            var response = new ResourceLog
            {
                CaasId = caasId,
                ResourceId = _resourceId,
                ResourceType = _resourceType,
                DeploymentStatus = ResourceLogStatus.Deleted
            };

            try
            {
                bool wait = await DeleteExistingResource(caasId);
                if (wait)
                {
                    await WaitForDelete(caasId);
                }
            }
            catch (CaasException ex)
            {
                _logProvider.LogError(ex.Message);

                response.DeploymentStatus = ResourceLogStatus.Failed;
                response.Error = new Error
                {
                    Message = ex.Message,
                    Operation = ex.Operation,
                    ResponseCode = ex.ResponseCode
                };
            }

            return response;
        }

        /// <summary>
        /// Deletes a resource by CaaS identifier.
        /// </summary>
        /// <param name="caasId">The CaaS identifier.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        private async Task<bool> DeleteExistingResource(string caasId)
        {
            _logProvider.LogMessage($"Deleting {_resourceType}: '{_resourceId}' (ID: {caasId}) ");
            using (var client = HttpClientFactory.GetClient(_accountDetails, "application/json"))
            {
                try
                {
                    var url = GetApiUrl(_resourceApi.DeleteUrl);
                    string jsonPayload = String.Format("{{ \"id\": \"{0}\" }}", caasId);
                    var response = await client.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                    response.ThrowForHttpFailure();
                    return true;
                }
                catch (CaasException ex)
                {
                    // Check detail
                    if (ex.ResponseCode == "RESOURCE_NOT_FOUND")
                    {
                        _logProvider.LogMessage("Not found.");
                        return false;
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets the absolute API URL.
        /// </summary>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <returns>The absolute URL.</returns>
        private string GetApiUrl(string relativeUrl)
        {
            return _accountDetails.BaseUrl + Mcp2UrlStem + "/" + _accountDetails.OrgId + relativeUrl;
        }

        /// <summary>
        /// Waits for deployment operation.
        /// </summary>
        /// <param name="caasId">The CaaS identifier.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        private async Task<JObject> WaitForDeploy(string caasId)
        {
            DateTime startTime = DateTime.Now;
            while (true)
            {
                if ((DateTime.Now - startTime).TotalMinutes >= PollingTimeOutMinutes)
                {
                    throw new TimeoutException(String.Format(
                        "Timed out waiting to create {0} with id {1}", _resourceType, caasId));
                }

                var props = await Get(caasId);
                if (props["state"].Value<string>() == "NORMAL")
                {
                    _logProvider.CompleteProgress();
                    return props;
                }
                _logProvider.IncrementProgress();
                await Task.Delay(TimeSpan.FromSeconds(PollingDelaySeconds));
            }
        }

        /// <summary>
        /// Waits for a delete operation.
        /// </summary>
        /// <param name="caasId">The CaaS identifier.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        private async Task WaitForDelete(string caasId)
        {
            DateTime startTime = DateTime.Now;
            while (true)
            {
                if ((DateTime.Now - startTime).TotalMinutes >= PollingTimeOutMinutes)
                {
                    throw new TimeoutException(String.Format(
                        "Timed out waiting to delete {0} with id {1}", _resourceType, caasId));
                }

                try
                {
                    var props = await Get(caasId);
                }
                catch (CaasException ex)
                {
                    if (ex.ResponseCode == "RESOURCE_NOT_FOUND")
                    {
                        _logProvider.CompleteProgress();
                        return;
                    }
                    throw;
                }

                _logProvider.IncrementProgress();
                await Task.Delay(TimeSpan.FromSeconds(PollingDelaySeconds));
            }
        }

        /// <summary>
        /// Retrieves the values from the template resource definition that can be used to uniquely identify an
        /// already deployed resource, in the order specified in the ListUrl parameters. 
        /// </summary>
        /// <param name="resourceDefinition">The JSON resource definition from the template</param>
        /// <returns>The list of parameter values to be used for the List API call</returns>
        private string[] GetResourceIdentifiers(JObject resourceDefinition)
        {
            switch (_resourceType)
            {
                case ResourceType.Vlan:
                case ResourceType.FirewallRule:
                case ResourceType.VirtualListener:
                case ResourceType.Pool:
                case ResourceType.Node:
                    return new[] { resourceDefinition["networkDomainId"].Value<string>(), resourceDefinition["name"].Value<string>() };
                case ResourceType.PoolMember:
                    return new[] { resourceDefinition["poolId"].Value<string>(), resourceDefinition["nodeId"].Value<string>(), };
                case ResourceType.NatRule:
                    return new[] { resourceDefinition["networkDomainId"].Value<string>(), resourceDefinition["internalIp"].Value<string>() };
                case ResourceType.Server:
                    return new[] { resourceDefinition["networkInfo"]["networkDomainId"].Value<string>(), resourceDefinition["name"].Value<string>() };
                default:
                    return new[] { resourceDefinition["name"].Value<string>() };
            }
        }

        /// <summary>
        /// Gets multiple resources by identifiers.
        /// </summary>
        /// <param name="ids">The ids.</param>
        /// <returns>The resources</returns>
        private async Task<IEnumerable<JObject>> GetResourceByIdentifiers(string[] ids)
        {
            if (_resourceApi.ListUrl == null)
            {
                // Some resource types can't be retrieved just by name
                return null;
            }

            using (var client = HttpClientFactory.GetClient(_accountDetails, "application/json"))
            {
                var url = String.Format(GetApiUrl(_resourceApi.ListUrl), ids);
                var response = await client.GetAsync(url);
                response.ThrowForHttpFailure();
                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);
                var results = (JArray)jsonResponse.First.Children().First();
                return results.Select(jv => (JObject)jv).ToArray();
            }
        }
    }
}
