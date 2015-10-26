﻿using DD.CBU.CaasDeploy.Library.Tasks;

namespace DD.CBU.CaasDeploy.Library.Contracts
{
    /// <summary>
    /// Builds task lists and contexts from deployment template documents.
    /// </summary>
    public interface ITemplateParser
    {
        /// <summary>
        /// Gets the deployment tasks for the supplied deployment template.
        /// </summary>
        /// <param name="templateFilePath">The template file path.</param>
        /// <param name="parametersFilePath">The parameters file path.</param>
        /// <returns>Instance of <see cref="TaskExecutor"/> with tasks and task execution context.</returns>
        TaskExecutor ParseDeploymentTemplate(string templateFilePath, string parametersFilePath);

        /// <summary>
        /// Gets the deletion tasks for the supplied deployment log.
        /// </summary>
        /// <param name="deploymentLogFilePath">The deployment log file path.</param>
        /// <returns>Instance of <see cref="TaskExecutor"/> with tasks and task execution context.</returns>
        TaskExecutor ParseDeploymentLog(string deploymentLogFilePath);
    }
}