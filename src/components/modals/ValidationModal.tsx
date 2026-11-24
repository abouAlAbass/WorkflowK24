import React from 'react';
import { X, AlertTriangle, XCircle, CheckCircle } from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface ValidationModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const ValidationModal: React.FC<ValidationModalProps> = ({ isOpen, onClose }) => {
  const { validateWorkflow, setSelectedNodeId } = useWorkflowStore();

  if (!isOpen) return null;

  const issues = validateWorkflow();
  const errors = issues.filter(i => i.type === 'error');
  const warnings = issues.filter(i => i.type === 'warning');

  const handleSelectIssueNode = (nodeId?: string) => {
    if (nodeId) {
      setSelectedNodeId(nodeId);
      onClose();
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4 select-none animate-fade-in text-[#0F172A] dark:text-slate-100">
      <div className="border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden flex flex-col transition-colors">
        {/* Header */}
        <div className="p-4 border-b border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            <h2 className="font-bold text-sm text-[#0F172A] dark:text-slate-100">Workflow Validation Report</h2>
          </div>
          <button
            onClick={onClose}
            aria-label="Close validation modal"
            className="p-1.5 rounded-lg text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-100 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-5 space-y-4 max-h-[60vh] overflow-y-auto">
          {issues.length === 0 ? (
            <div className="p-6 text-center space-y-2 rounded-2xl border border-emerald-200 dark:border-emerald-800 bg-[#ECFDF5]/70 dark:bg-emerald-950/40">
              <CheckCircle className="w-10 h-10 text-[#059669] dark:text-emerald-400 mx-auto" />
              <h3 className="text-sm font-bold text-[#059669] dark:text-emerald-400">All Checks Passed!</h3>
              <p className="text-xs text-[#475569] dark:text-slate-300 font-medium">
                Your workflow schema is complete with valid trigger, termination paths, assignees, and outcomes.
              </p>
            </div>
          ) : (
            <>
              {errors.length > 0 && (
                <div className="space-y-2">
                  <span className="text-xs font-bold text-[#DC2626] dark:text-rose-400 uppercase tracking-wider block">
                    Errors ({errors.length}) - Must Fix
                  </span>
                  <div className="space-y-2">
                    {errors.map((issue) => (
                      <div
                        key={issue.id}
                        onClick={() => handleSelectIssueNode(issue.nodeId)}
                        className={`p-3 rounded-2xl border border-red-200 dark:border-red-900 bg-[#FEF2F2] dark:bg-rose-950/40 flex items-start gap-2.5 transition-colors ${issue.nodeId ? 'cursor-pointer hover:bg-red-100 dark:hover:bg-rose-900/60' : ''}`}
                      >
                        <XCircle className="w-4 h-4 text-[#DC2626] dark:text-rose-400 shrink-0 mt-0.5" />
                        <div className="flex-1">
                          <p className="text-xs font-bold text-[#0F172A] dark:text-slate-100">{issue.message}</p>
                          {issue.nodeId && (
                            <span className="text-[10px] text-[#DC2626] dark:text-rose-400 hover:underline block mt-0.5 font-bold">
                              Click to select node in editor
                            </span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {warnings.length > 0 && (
                <div className="space-y-2">
                  <span className="text-xs font-bold text-[#D97706] dark:text-amber-400 uppercase tracking-wider block">
                    Warnings ({warnings.length})
                  </span>
                  <div className="space-y-2">
                    {warnings.map((issue) => (
                      <div
                        key={issue.id}
                        onClick={() => handleSelectIssueNode(issue.nodeId)}
                        className={`p-3 rounded-2xl border border-amber-200 dark:border-amber-900 bg-[#FFFBEB] dark:bg-amber-950/40 flex items-start gap-2.5 transition-colors ${issue.nodeId ? 'cursor-pointer hover:bg-amber-100 dark:hover:bg-amber-900/60' : ''}`}
                      >
                        <AlertTriangle className="w-4 h-4 text-[#D97706] dark:text-amber-400 shrink-0 mt-0.5" />
                        <div className="flex-1">
                          <p className="text-xs font-bold text-[#0F172A] dark:text-slate-100">{issue.message}</p>
                          {issue.nodeId && (
                            <span className="text-[10px] text-[#D97706] dark:text-amber-400 hover:underline block mt-0.5 font-bold">
                              Click to select node in editor
                            </span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 bg-slate-200 dark:bg-slate-800 text-[#0F172A] dark:text-slate-200 hover:bg-slate-300 dark:hover:bg-slate-700 rounded-xl text-xs font-bold shadow-2xs"
          >
            Close Report
          </button>
        </div>
      </div>
    </div>
  );
};
