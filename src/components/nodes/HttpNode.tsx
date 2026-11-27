import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { Globe, Link2, ShieldAlert } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const HttpNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const http = customData.httpData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const method = http?.method || 'GET';
  const methodBadgeColor =
    method === 'POST'
      ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/50 dark:text-emerald-300'
      : method === 'PUT' || method === 'PATCH'
      ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/50 dark:text-amber-300'
      : method === 'DELETE'
      ? 'bg-rose-100 text-rose-800 dark:bg-rose-900/50 dark:text-rose-300'
      : 'bg-cyan-100 text-cyan-800 dark:bg-cyan-900/50 dark:text-cyan-300';

  return (
    <div
      className={`relative min-w-[280px] max-w-[320px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#0891B2] transition-all duration-200 shadow-lg shadow-cyan-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive
          ? 'ring-4 ring-cyan-400 scale-105 shadow-cyan-200'
          : selected
            ? 'ring-4 ring-cyan-500/30 shadow-cyan-100 dark:shadow-cyan-950'
            : 'hover:shadow-xl hover:border-cyan-600'
      }`}
    >
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-[#0891B2] !border-2 !border-white hover:!bg-cyan-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow step"
        aria-label="Inbound target handle"
      />

      <div className="flex items-center justify-between p-3.5 border-b border-cyan-100 dark:border-cyan-900/60 bg-[#ECFEFF] dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#0891B2] dark:text-cyan-400 border border-cyan-200 dark:border-cyan-800 shadow-sm shrink-0">
            <Globe className="w-5 h-5 text-[#0891B2] dark:text-cyan-400" />
          </div>
          <div className="min-w-0">
            <span className="text-xs font-extrabold tracking-wider text-[#0891B2] dark:text-cyan-400 uppercase">
              HTTP / REST
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[180px]">
              {customData.label || http?.label || 'HTTP Request'}
            </h3>
          </div>
        </div>
        <span className={`px-2 py-0.5 rounded-md text-[10px] font-mono font-extrabold shrink-0 ${methodBadgeColor}`}>
          {method}
        </span>
      </div>

      <div className="p-3.5 space-y-2.5 text-xs">
        <div className="flex items-center justify-between p-2 rounded-xl bg-cyan-50/50 dark:bg-slate-800/60 border border-cyan-100 dark:border-cyan-900">
          <div className="flex items-center gap-1.5 text-[#475569] dark:text-slate-300 shrink-0">
            <Link2 className="w-3.5 h-3.5 text-[#0891B2] dark:text-cyan-400" />
            <span className="font-medium">Conn:</span>
          </div>
          <span className="font-mono font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[170px]" title={http?.connectionRef}>
            {http?.connectionRef || '(not selected)'}
          </span>
        </div>

        <div className="p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800">
          <p className="font-mono text-[11px] text-[#0F172A] dark:text-slate-100 truncate font-semibold" title={http?.path}>
            {http?.path || '/'}
          </p>
        </div>

        {http?.onBusinessError?.routeTo && (
          <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-[11px] text-amber-800 dark:text-amber-300 font-semibold">
            <ShieldAlert className="w-3.5 h-3.5 shrink-0" />
            <span className="truncate">4xx -&gt; {http.onBusinessError.routeTo}</span>
          </div>
        )}
      </div>

      <Handle
        type="source"
        position={Position.Right}
        id="http-out"
        className="!w-3.5 !h-3.5 !bg-[#0891B2] !border-2 !border-white hover:!bg-cyan-700 !-right-2 transition-transform hover:scale-125 cursor-pointer shadow-sm"
        title="Connect downstream step"
        aria-label="HTTP outbound connection handle"
      />
    </div>
  );
});

HttpNode.displayName = 'HttpNode';
