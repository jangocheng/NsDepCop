﻿using System;
using System.Collections.Generic;
using Codartis.NsDepCop.Core.Interface.Analysis;
using Codartis.NsDepCop.Core.Interface.Analysis.Messages;
using Codartis.NsDepCop.Core.Interface.Config;
using Codartis.NsDepCop.Core.Util;

namespace Codartis.NsDepCop.Core.Implementation.Analysis
{
    /// <summary>
    /// Abstract base class for dependency analyzers.
    /// </summary>
    public abstract class DependencyAnalyzerBase : IDependencyAnalyzer
    {
        protected readonly IUpdateableConfigProvider ConfigProvider;
        protected readonly MessageHandler TraceMessageHandler;

        protected DependencyAnalyzerBase(IUpdateableConfigProvider configProvider, MessageHandler traceMessageHandler)
        {
            ConfigProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            TraceMessageHandler = traceMessageHandler;
        }

        public Importance InfoImportance => ConfigProvider.InfoImportance;

        public abstract IEnumerable<AnalyzerMessageBase> AnalyzeProject(IEnumerable<string> sourceFilePaths, IEnumerable<string> referencedAssemblyPaths);
        public abstract IEnumerable<AnalyzerMessageBase> AnalyzeSyntaxNode(ISyntaxNode syntaxNode, ISemanticModel semanticModel);
        public abstract void RefreshConfig();

        protected IEnumerable<AnalyzerMessageBase> AnalyzeCore(Func<IEnumerable<TypeDependency>> illegalTypeDependencyEnumerator, bool isProjectScope)
        {
            switch (ConfigProvider.ConfigState)
            {
                case AnalyzerConfigState.NoConfig:
                    yield return new NoConfigFileMessage();
                    break;

                case AnalyzerConfigState.Disabled:
                    yield return new ConfigDisabledMessage();
                    break;

                case AnalyzerConfigState.ConfigError:
                    yield return new ConfigErrorMessage(ConfigProvider.ConfigException);
                    break;

                case AnalyzerConfigState.Enabled:
                    var messages = PerformAnalysis(illegalTypeDependencyEnumerator, isProjectScope);
                    foreach (var message in messages)
                        yield return message;
                    break;

                default:
                    throw new Exception($"Unexpected ConfigState: {ConfigProvider.ConfigState}");
            }
        }

        private IEnumerable<AnalyzerMessageBase> PerformAnalysis(Func<IEnumerable<TypeDependency>> illegalTypeDependencyEnumerator, bool isProjectScope)
        {
            var config = ConfigProvider.Config;
            var maxIssueCount = config.MaxIssueCount;

            var startTime = DateTime.Now;
            yield return new AnalysisStartedMessage(ConfigProvider.ConfigLocation);

            var issueCount = 0;
            foreach (var illegalDependency in illegalTypeDependencyEnumerator())
            {
                issueCount++;

                if (issueCount > maxIssueCount)
                {
                    yield return new TooManyIssuesMessage(maxIssueCount, config.MaxIssueCountSeverity);
                    break;
                }

                yield return new IllegalDependencyMessage(illegalDependency, config.IssueKind);
            }

            var endTime = DateTime.Now;
            yield return new AnalysisFinishedMessage(endTime - startTime);

            if (isProjectScope && config.AutoLowerMaxIssueCount && issueCount < maxIssueCount)
                ConfigProvider.UpdateMaxIssueCount(issueCount);
        }
    }
}