using System;
using System.Collections.Generic;
using System.Threading;
using FigmaToUnity.Editor.ImportPipeline;
using FigmaToUnity.Editor.State;

namespace FigmaToUnity.Editor.SharedPipeline
{
    // Carries everything a backend needs to import a pre-tagged, pre-hashed, pre-mapped
    // tree without touching FigmaImportController internals.
    internal sealed class ImportContext
    {
        public ImportContext(
            FigmaImportSession session,
            List<FigmaNode> importedRootNodes,
            ImportModeKind importMode,
            Action<ImportProgress> reportProgress,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            Session = session;
            ImportedRootNodes = importedRootNodes;
            ImportMode = importMode;
            ReportProgress = reportProgress;
            Log = log;
            CancellationToken = cancellationToken;
        }

        public FigmaImportSession Session { get; }
        public List<FigmaNode> ImportedRootNodes { get; }
        public ImportModeKind ImportMode { get; }
        public Action<ImportProgress> ReportProgress { get; }
        public Action<string> Log { get; }
        public CancellationToken CancellationToken { get; }

        public void Report(string stage, string status, int current, int total, bool indeterminate, bool working)
        {
            ReportProgress?.Invoke(new ImportProgress(stage, status, current, total, indeterminate, working));
        }
    }
}
