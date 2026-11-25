import React, { useEffect, useState, useCallback } from 'react';
import {
  X,
  PauseCircle,
  PlayCircle,
  RotateCcw,
  XCircle,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  Clock,
  Activity,
  ShieldAlert,
  Layers
} from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import type { ViewFlowData, ViewFlowNodeExecution } from '../../types/workflow';

interface InstanceViewFlowPanelProps {
  instanceId: string;
  onClose: () => void;
  onStatusChanged?: () => void;
}

const MIN_CANCEL_REASON_LENGTH = 10;

function formatNodeDuration(node: ViewFlowNodeExecution): string {
  const start = new Date(node.startedAt).getTime();
  if (Number.isNaN(start)) return '—';
  const end = node.endedAt ? new Date(node.endedAt).getTime() : Date.now();
  const diffMs = Math.max(0, end - start);
  if (diffMs < 1000) return `${diffMs}ms`;
  const totalSec = Math.floor(diffMs / 1000);
  if (totalSec < 60) return `${totalSec}s`;
  const mins = Math.floor(totalSec / 60);
  const secs = totalSec % 60;
  return `${mins}m ${secs}s`;
}

function formatApiError(err: unknown): string {
  const apiErr = err as Error & { status?: number; code?: string };
  if (apiErr?.status === 403) {
    return `403 Forbidden: ${apiErr.message || 'You do not have permission to execute this operational command.'}`;
  }
  if (apiErr?.status === 409) {
    return `409 Conflict (${apiErr.code || 'CONFLICT'}): ${apiErr.message || 'Invalid state transition or concurrent modification.'}`;
  }
  return apiErr?.message || 'An unexpected error occurred while executing the operation.';
}

export const InstanceViewFlowPanel: React.FC<InstanceViewFlowPanelProps> = ({
  instanceId,
  onClose,
  onStatusChanged
}) => {
  const [flow, setFlow] = useState<ViewFlowData | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyCommand, setBusyCommand] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Cancel modal state
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelTouched, setCancelTouched] = useState(false);

  const loadFlow = useCallback(async () => {
    setLoading(true);
    setErrorMessage(null);
    try {
      const repo = getWorkflowRepository();
      const data = await repo.getInstanceFlow(instanceId);
      setFlow(data);
    } catch (err) {
      setErrorMessage(formatApiError(err));
    } finally {
      setLoading(false);
    }
  }, [instanceId]);

  useEffect(() => {
    void loadFlow();
  }, [loadFlow]);

  const normalizedStatus = (flow?.instance.status || '').toUpperCase();
  const canSuspend = normalizedStatus === 'RUNNING' || normalizedStatus === 'WAITING' || normalizedStatus === 'PENDING';
  const canResume = normalizedStatus === 'SUSPENDED';
  const canRetry = normalizedStatus === 'FAULTED' || normalizedStatus === 'FAILED';
  const isTerminal = normalizedStatus === 'COMPLETED' || normalizedStatus === 'CANCELLED';
  const canCancel = Boolean(flow) && !isTerminal;

  const trimmedCancelReason = cancelReason.trim();
  const isCancelReasonValid = trimmedCancelReason.length >= MIN_CANCEL_REASON_LENGTH;
  const cancelValidationError = cancelTouched && !isCancelReasonValid
    ? `Cancellation reason must be at least ${MIN_CANCEL_REASON_LENGTH} characters (${trimmedCancelReason.length}/${MIN_CANCEL_REASON_LENGTH}).`
    : null;

  const executeCommand = async (commandName: string, action: () => Promise<void>) => {
    setBusyCommand(commandName);
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      await action();
      setSuccessMessage(`${commandName} command executed successfully.`);
      await loadFlow();
      onStatusChanged?.();
    } catch (err) {
      setErrorMessage(formatApiError(err));
    } finally {
      setBusyCommand(null);
    }
  };

  const handleSuspend = () =>
    executeCommand('Suspend', async () => {
      await getWorkflowRepository().suspendInstance(instanceId, 'Suspended from ViewFlow console');
    });

  const handleResume = () =>
    executeCommand('Resume', async () => {
      await getWorkflowRepository().resumeInstance(instanceId);
    });

  const handleRetry = () =>
    executeCommand('Retry', async () => {
      await getWorkflowRepository().retryInstance(instanceId, 'retry_failed_node');
    });

  const handleConfirmCancel = async (e: React.FormEvent) => {
    e.preventDefault();
    setCancelTouched(true);
    if (!isCancelReasonValid) {
      return;
    }

    await executeCommand('Cancel', async () => {
      await getWorkflowRepository().cancelInstance(instanceId, trimmedCancelReason);
      setShowCancelModal(false);
      setCancelReason('');
      setCancelTouched(false);
    });
  };

  const renderNodeStatusBadge = (status: string) => {
    const s = status.toUpperCase();
    if (s === 'COMPLETED') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
          <CheckCircle2 className="w-3 h-3" />
          Completed
        </span>
      );
    }
    if (s === 'FAILED') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-50 dark:bg-red-950/60 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-800">
          <AlertTriangle className="w-3 h-3" />
          Failed
        </span>
      );
    }
    if (s === 'WAITING') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
          <Clock className="w-3 h-3" />
          Waiting
        </span>
      );
    }
    if (s === 'CANCELLED') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border border-slate-300 dark:border-slate-700">
          <XCircle className="w-3 h-3" />
          Cancelled
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-blue-50 dark:bg-blue-950/60 text-blue-700 dark:text-blue-400 border border-blue-200 dark:border-blue-800">
        <Activity className="w-3 h-3" />
        {status}
      </span>
    );
  };

  return (
    <section
      aria-label={`ViewFlow Inspector for ${instanceId}`}
      className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-md space-y-5 transition-colors"
    >
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-[#E2E8F0] dark:border-[#334155] pb-4">
        <div>
          <div className="flex items-center gap-2 flex-wrap">
            <Layers className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            <h3 className="text-base font-extrabold text-[#0F172A] dark:text-white">
              ViewFlow & Operational Control
            </h3>
            <span className="font-mono text-xs px-2 py-0.5 rounded bg-blue-50 dark:bg-blue-950 text-[#2563EB] dark:text-blue-400 border border-blue-200 dark:border-blue-800">
              {instanceId}
            </span>
            {flow && (
              <span
                data-testid="viewflow-status-badge"
                className="px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-slate-100 dark:bg-slate-800 text-[#0F172A] dark:text-slate-200 border border-slate-300 dark:border-slate-700"
              >
                {flow.instance.status}
              </span>
            )}
          </div>
          {flow && (
            <p className="text-[11px] text-[#64748B] dark:text-slate-400 mt-1">
              Version v{flow.instance.version} • Def #{flow.instance.definitionId} (v{flow.instance.definitionVersion}) • Started{' '}
              {new Date(flow.instance.startedAt).toLocaleString()}
              {flow.instance.suspendedFromStatus ? ` • Suspended from ${flow.instance.suspendedFromStatus}` : ''}
            </p>
          )}
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          {canSuspend && (
            <button
              type="button"
              onClick={handleSuspend}
              disabled={busyCommand !== null}
              className="px-3 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-xs font-bold inline-flex items-center gap-1.5 cursor-pointer transition-colors"
            >
              <PauseCircle className="w-3.5 h-3.5" />
              Suspend
            </button>
          )}

          {canResume && (
            <button
              type="button"
              onClick={handleResume}
              disabled={busyCommand !== null}
              className="px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white text-xs font-bold inline-flex items-center gap-1.5 cursor-pointer transition-colors"
            >
              <PlayCircle className="w-3.5 h-3.5" />
              Resume
            </button>
          )}

          {canRetry && (
            <button
              type="button"
              onClick={handleRetry}
              disabled={busyCommand !== null}
              className="px-3 py-1.5 rounded-xl bg-[#2563EB] hover:bg-blue-700 disabled:opacity-50 text-white text-xs font-bold inline-flex items-center gap-1.5 cursor-pointer transition-colors"
            >
              <RotateCcw className="w-3.5 h-3.5" />
              Retry
            </button>
          )}

          {canCancel && (
            <button
              type="button"
              onClick={() => {
                setShowCancelModal(true);
                setCancelTouched(false);
                setErrorMessage(null);
              }}
              disabled={busyCommand !== null}
              className="px-3 py-1.5 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white text-xs font-bold inline-flex items-center gap-1.5 cursor-pointer transition-colors"
            >
              <XCircle className="w-3.5 h-3.5" />
              Cancel
            </button>
          )}

          <button
            type="button"
            onClick={() => void loadFlow()}
            disabled={loading || busyCommand !== null}
            aria-label="Refresh ViewFlow"
            className="p-1.5 rounded-xl border border-[#E2E8F0] dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-[#64748B] dark:text-slate-300 cursor-pointer"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>

          <button
            type="button"
            onClick={onClose}
            aria-label="Close ViewFlow"
            className="p-1.5 rounded-xl border border-[#E2E8F0] dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-[#64748B] dark:text-slate-300 cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Error & Success Banners */}
      {errorMessage && (
        <div
          role="alert"
          className="p-3 rounded-xl bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300 text-xs font-semibold flex items-center gap-2"
        >
          <ShieldAlert className="w-4 h-4 shrink-0" />
          <span>{errorMessage}</span>
        </div>
      )}

      {successMessage && (
        <div
          role="status"
          className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 text-emerald-700 dark:text-emerald-300 text-xs font-semibold flex items-center gap-2"
        >
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{successMessage}</span>
        </div>
      )}

      {/* Cancel Confirmation Modal / Inline Form */}
      {showCancelModal && (
        <form
          onSubmit={handleConfirmCancel}
          aria-label="Cancel Instance Form"
          className="p-4 rounded-2xl bg-rose-50/70 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900 space-y-3"
        >
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-extrabold text-rose-800 dark:text-rose-300">
              Confirm Instance Cancellation
            </h4>
            <button
              type="button"
              onClick={() => setShowCancelModal(false)}
              className="text-xs font-bold text-rose-700 dark:text-rose-400 hover:underline cursor-pointer"
            >
              Dismiss
            </button>
          </div>

          <div className="space-y-1.5">
            <label
              htmlFor="cancel-instance-reason"
              className="block text-xs font-bold text-[#0F172A] dark:text-slate-200"
            >
              Cancellation Reason (minimum {MIN_CANCEL_REASON_LENGTH} characters)
            </label>
            <textarea
              id="cancel-instance-reason"
              rows={3}
              value={cancelReason}
              onChange={(e) => {
                setCancelReason(e.target.value);
                if (!cancelTouched) setCancelTouched(true);
              }}
              onBlur={() => setCancelTouched(true)}
              aria-invalid={cancelValidationError ? 'true' : 'false'}
              aria-describedby={cancelValidationError ? 'cancel-reason-error' : undefined}
              placeholder="Explain why this workflow instance is being cancelled..."
              className="w-full rounded-xl bg-white dark:bg-slate-900 border border-rose-300 dark:border-rose-800 p-2.5 text-xs text-[#0F172A] dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-rose-500"
            />
            {cancelValidationError && (
              <p id="cancel-reason-error" role="alert" className="text-[11px] font-semibold text-rose-700 dark:text-rose-400">
                {cancelValidationError}
              </p>
            )}
          </div>

          <div className="flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={() => setShowCancelModal(false)}
              className="px-3 py-1.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs font-bold text-[#0F172A] dark:text-slate-200 cursor-pointer"
            >
              Keep Running
            </button>
            <button
              type="submit"
              disabled={!isCancelReasonValid || busyCommand !== null}
              className="px-3 py-1.5 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white text-xs font-bold cursor-pointer"
            >
              Confirm Cancel
            </button>
          </div>
        </form>
      )}

      {loading && !flow ? (
        <div className="py-8 text-center text-xs font-semibold text-[#64748B] dark:text-slate-400">
          Loading ViewFlow projection...
        </div>
      ) : flow ? (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
          {/* Executed Nodes Projection */}
          <div className="space-y-3">
            <h4 className="text-xs font-extrabold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
              Executed Nodes ({flow.nodes.length})
            </h4>
            {flow.nodes.length === 0 ? (
              <p className="text-xs text-[#64748B] dark:text-slate-400">No node executions recorded yet.</p>
            ) : (
              <div className="space-y-2">
                {flow.nodes.map((node, idx) => (
                  <div
                    key={`${node.nodeId}-${idx}`}
                    className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-800/60 border border-[#E2E8F0] dark:border-slate-700 space-y-1.5"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-xs font-bold text-[#0F172A] dark:text-white">
                          {node.nodeId}
                        </span>
                        <span className="text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded bg-slate-200 dark:bg-slate-700 text-[#475569] dark:text-slate-300">
                          {node.type}
                        </span>
                      </div>
                      {renderNodeStatusBadge(node.status)}
                    </div>
                    <div className="flex items-center gap-4 text-[11px] text-[#64748B] dark:text-slate-400">
                      <span>Attempts: <strong className="text-[#0F172A] dark:text-slate-200">{node.attempts}</strong></span>
                      <span>Duration: <strong className="text-[#0F172A] dark:text-slate-200">{formatNodeDuration(node)}</strong></span>
                    </div>
                    {node.error && (
                      <div className="text-[11px] font-semibold text-rose-600 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/40 rounded-lg px-2 py-1">
                        {node.error}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* Instance Variables */}
            <div className="pt-2 space-y-2">
              <h4 className="text-xs font-extrabold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
                Context Variables
              </h4>
              {Object.keys(flow.variables || {}).length === 0 ? (
                <p className="text-xs text-[#64748B] dark:text-slate-400">No context variables.</p>
              ) : (
                <div className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-800/60 border border-[#E2E8F0] dark:border-slate-700 space-y-1.5">
                  {Object.entries(flow.variables).map(([key, val]) => (
                    <div key={key} className="flex items-center justify-between gap-2 text-xs">
                      <span className="font-mono font-semibold text-[#475569] dark:text-slate-300">{key}</span>
                      <span className="font-mono text-[#0F172A] dark:text-white truncate max-w-[60%]">
                        {typeof val === 'object' && val !== null ? JSON.stringify(val) : String(val ?? '')}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Event Timeline */}
          <div className="space-y-3">
            <h4 className="text-xs font-extrabold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
              Event Timeline ({flow.timeline.length})
            </h4>
            {flow.timeline.length === 0 ? (
              <p className="text-xs text-[#64748B] dark:text-slate-400">No domain events recorded yet.</p>
            ) : (
              <ol className="space-y-2">
                {flow.timeline.map((evt) => (
                  <li
                    key={`${evt.id}-${evt.seq}`}
                    className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-800/60 border border-[#E2E8F0] dark:border-slate-700 flex items-start justify-between gap-3"
                  >
                    <div className="space-y-0.5">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-[10px] font-bold px-1.5 py-0.5 rounded bg-blue-50 dark:bg-blue-950 text-[#2563EB] dark:text-blue-400">
                          #{evt.seq}
                        </span>
                        <span className="text-xs font-bold text-[#0F172A] dark:text-white">{evt.type}</span>
                      </div>
                      <div className="text-[11px] text-[#64748B] dark:text-slate-400">
                        Actor: <strong className="text-[#0F172A] dark:text-slate-200">{evt.actorId || evt.actorType}</strong>
                        {evt.nodeId ? ` • Node: ${evt.nodeId}` : ''}
                      </div>
                    </div>
                    <time className="text-[10px] font-mono text-[#64748B] dark:text-slate-400 shrink-0">
                      {new Date(evt.createdAt).toLocaleString()}
                    </time>
                  </li>
                ))}
              </ol>
            )}
          </div>
        </div>
      ) : null}
    </section>
  );
};
