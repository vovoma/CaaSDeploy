﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CaasDeploy.Library.Contracts;
using CaasDeploy.Library.Models;
using CaasDeploy.Library.Utilities;

namespace CaasDeploy.Library.Tasks
{
    /// <summary>
    /// An implementation of <see cref="ITask"/> which loads existing resources.
    /// </summary>
    internal sealed class LoadExistingResourcesTask : ITask
    {
        private readonly CaasAccountDetails _accountDetails;
        private readonly ILogProvider _logProvider;
        private readonly IList<ExistingResource> _existingResources;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadExistingResourcesTask"/> class.
        /// </summary>
        /// <param name="accountDetails">The CaaS account details.</param>
        /// <param name="logProvider">The log provider.</param>
        /// <param name="existingResources">The existing resources to load.</param>
        public LoadExistingResourcesTask(CaasAccountDetails accountDetails, ILogProvider logProvider, IList<ExistingResource> existingResources)
        {
            if (accountDetails == null)
            {
                throw new ArgumentNullException(nameof(accountDetails));
            }

            if (logProvider == null)
            {
                throw new ArgumentNullException(nameof(logProvider));
            }

            if (existingResources == null)
            {
                throw new ArgumentNullException(nameof(existingResources));
            }

            _accountDetails = accountDetails;
            _logProvider = logProvider;
            _existingResources = existingResources;
        }

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <param name="context">The task execution context.</param>
        /// <returns>The async <see cref="Task"/>.</returns>
        public async Task Execute(TaskContext context)
        {
            if (_existingResources == null)
            {
                return;
            }

            foreach (var existingResource in _existingResources)
            {
                existingResource.caasId = TokenHelper.SubstitutePropertyTokensInString(existingResource.caasId, context.Parameters);
                var deployer = new ResourceDeployer(existingResource.resourceId, existingResource.resourceType, _accountDetails, _logProvider);
                var resource = await deployer.Get(existingResource.caasId);
                context.ResourcesProperties.Add(existingResource.resourceId, resource);
            }
        }
    }
}
