import React from 'react';
import { StepForward, Square, Terminal, CheckCircle2 } from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const SimulatorPanel: React.FC = () => {
  const { 
    isSimulating, 
    activeSimNodeId, 
    simulationLogs, 
    stepSimulation, 
    stopSimulation,
    nodes 
  } = useWorkflowStore();

  if (!isSimulating) return null;

  const activeNode = nodes.find(n => n.id === activeSimNodeId);

  return (
    <div className="absolute bottom-4 left-72 right-80 mx-auto z-40 bg-white/95 dark:bg-slate-900/95 border-2 border-amber-300 dark:border-amber-700 rounded-2xl shadow-2xl backdrop-blur-md p-3.5 select-none text-[#0F172A] dark:text-slate-100 transition-colors">
      {/* Console Header */}
      <div className="flex items-center justify-between pb-2 mb-2 border-b border-slate-200 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <div className="p-1 rounded-lg bg-amber-100 dark:bg-amber-950/60 text-[#D97706] dark:text-amber-400 border border-amber-200 dark:border-amber-800 shadow-2xs">
            <Terminal className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-xs font-extrabold flex items-center gap-2 text-[#0F172A] dark:text-slate-100">
              Execution Simulation Console
              <span className="w-2 h-2 rounded-full bg-amber-500 animate-ping" />
            </h3>
            <span className="text-[10px] text-[#475569] dark:text-slate-400 font-medium">
              Active Step: {activeNode ? `"${activeNode.data.label}" (${activeNode.id})` : 'Workflow Completed'}
            </span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {activeSimNodeId ? (
            <button
              onClick={stepSimulation}
              className="px-3 py-1.5 bg-[#D97706] hover:bg-amber-700 text-white rounded-xl text-xs font-bold flex items-center gap-1 shadow-2xs transition-colors"
            >
              <StepForward className="w-3.5 h-3.5" />
              <span>Step Forward</span>
            </button>
          ) : (
            <span className="text-xs text-[#059669] dark:text-emerald-400 font-bold flex items-center gap-1 px-2.5 py-1 bg-emerald-50 dark:bg-emerald-950/40 rounded-xl border border-emerald-200 dark:border-emerald-800">
              <CheckCircle2 className="w-3.5 h-3.5" />
              Finished Execution
            </span>
          )}

          <button
            onClick={stopSimulation}
            aria-label="Close execution simulation console"
            className="px-2.5 py-1.5 bg-white dark:bg-slate-800 hover:bg-rose-50 dark:hover:bg-rose-950/50 border border-slate-300 dark:border-slate-700 text-[#475569] dark:text-slate-200 hover:text-[#DC2626] dark:hover:text-rose-400 rounded-xl text-xs font-bold flex items-center gap-1 shadow-2xs transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-rose-500"
          >
            <Square className="w-3.5 h-3.5 text-[#DC2626]" />
            <span>Close Sim</span>
          </button>
        </div>
      </div>

      {/* Logs Feed */}
      <div className="max-h-32 overflow-y-auto space-y-1 font-mono text-[11px] p-2.5 rounded-xl border border-slate-800 bg-[#0F172A] text-slate-200 shadow-inner">
        {simulationLogs.map((log, idx) => (
          <div key={idx} className="flex items-start gap-2 leading-relaxed">
            <span className="text-slate-400 shrink-0">[{log.timestamp}]</span>
            <span className="font-bold text-indigo-400 shrink-0">[{log.nodeName}]:</span>
            <span className={log.status === 'success' ? 'text-emerald-400' : 'text-slate-200'}>
              {log.message}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
};
