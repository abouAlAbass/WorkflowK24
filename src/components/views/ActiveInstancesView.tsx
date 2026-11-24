import React, { useState } from 'react';
import { Activity, CheckCircle2, Clock, AlertTriangle, Search, Play } from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import type { InstanceStatus } from '../../types/workflow';

export const ActiveInstancesView: React.FC = () => {
  const { activeInstances, setCurrentTab, loadTemplate } = useWorkflowStore();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');

  const filteredInstances = activeInstances.filter(inst => {
    const matchesSearch = inst.workflowName.toLowerCase().includes(search.toLowerCase()) || 
                          inst.id.toLowerCase().includes(search.toLowerCase()) ||
                          inst.requester.toLowerCase().includes(search.toLowerCase());
    const matchesStatus = statusFilter === 'ALL' || inst.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const renderStatusBadge = (status: InstanceStatus) => {
    switch (status) {
      case 'RUNNING':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold bg-blue-50 dark:bg-blue-950/60 text-blue-700 dark:text-blue-400 border border-blue-200 dark:border-blue-800">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-ping" />
            RUNNING
          </span>
        );
      case 'COMPLETED':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
            <CheckCircle2 className="w-3 h-3" />
            COMPLETED
          </span>
        );
      case 'FAILED':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold bg-red-50 dark:bg-red-950/60 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-800">
            <AlertTriangle className="w-3 h-3" />
            FAILED
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
            <Clock className="w-3 h-3" />
            PENDING
          </span>
        );
    }
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Activity className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            Active Process Instances & Real-Time Monitoring
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Monitor running, completed, pending, and failed workflow executions across all environments
          </p>
        </div>

        <button
          onClick={() => {
            loadTemplate('onboarding');
            setCurrentTab('designer');
          }}
          className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-md cursor-pointer transition-all"
        >
          <Play className="w-4 h-4 fill-white" />
          <span>Launch New Instance</span>
        </button>
      </div>

      {/* Control Filters */}
      <div className="p-4 rounded-2xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4 transition-colors">
        <div className="relative flex-1 max-w-md">
          <Search className="w-4 h-4 text-[#94A3B8] absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Filter instances by name, ID, or requester..."
            className="w-full bg-[#F1F5F9] dark:bg-slate-800/80 border border-[#E2E8F0] dark:border-slate-700 rounded-xl pl-10 pr-4 py-2 text-xs text-[#0F172A] dark:text-slate-100 placeholder-[#94A3B8] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
          />
        </div>

        <div className="flex items-center gap-1.5 p-1 bg-[#F1F5F9] dark:bg-slate-800 rounded-xl border border-[#E2E8F0] dark:border-slate-700">
          {['ALL', 'RUNNING', 'PENDING', 'COMPLETED', 'FAILED'].map((st) => (
            <button
              key={st}
              onClick={() => setStatusFilter(st)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-colors cursor-pointer ${
                statusFilter === st
                  ? 'bg-white dark:bg-slate-900 text-[#2563EB] dark:text-blue-400 shadow-2xs'
                  : 'text-[#64748B] dark:text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-200'
              }`}
            >
              {st}
            </button>
          ))}
        </div>
      </div>

      {/* Table Card */}
      <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs overflow-hidden transition-colors">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-[#E2E8F0] dark:border-[#334155] text-[11px] font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
                <th className="pb-3 px-3">Instance ID & Name</th>
                <th className="pb-3 px-3">Requester</th>
                <th className="pb-3 px-3">Current Step</th>
                <th className="pb-3 px-3">Execution Progress</th>
                <th className="pb-3 px-3">Status</th>
                <th className="pb-3 px-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E8F0] dark:divide-[#334155] text-xs">
              {filteredInstances.map((inst) => (
                <tr key={inst.id} className="group hover:bg-[#F8FAFC] dark:hover:bg-slate-800/60 transition-colors">
                  <td className="py-4 px-3">
                    <div className="font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
                      <span className="font-mono text-[11px] text-[#2563EB] dark:text-blue-400 bg-blue-50 dark:bg-blue-950 px-2 py-0.5 rounded border border-blue-200 dark:border-blue-800">
                        {inst.id}
                      </span>
                      <span>{inst.workflowName}</span>
                    </div>
                    <span className="text-[11px] text-[#64748B] dark:text-slate-400 block mt-1">
                      Started {inst.startedAt} • Runtime {inst.duration}
                    </span>
                  </td>

                  <td className="py-4 px-3">
                    <div className="flex items-center gap-2">
                      <img
                        src={inst.requesterAvatar}
                        alt={inst.requester}
                        className="w-6 h-6 rounded-full object-cover border border-slate-200 dark:border-slate-700"
                      />
                      <div>
                        <div className="font-bold text-[#0F172A] dark:text-slate-200">{inst.requester}</div>
                        <div className="text-[10px] text-[#64748B] dark:text-slate-400">{inst.department}</div>
                      </div>
                    </div>
                  </td>

                  <td className="py-4 px-3 font-semibold text-[#0F172A] dark:text-slate-200">
                    {inst.currentStep}
                  </td>

                  <td className="py-4 px-3 w-48">
                    <div className="space-y-1">
                      <div className="flex items-center justify-between text-[10px] font-bold text-[#64748B] dark:text-slate-400 font-mono">
                        <span>Progress</span>
                        <span>{inst.progress}%</span>
                      </div>
                      <div className="w-full h-2 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                        <div
                          className={`h-full rounded-full transition-all ${
                            inst.status === 'FAILED' 
                              ? 'bg-rose-500' 
                              : inst.status === 'COMPLETED' 
                                ? 'bg-emerald-500' 
                                : 'bg-[#2563EB]'
                          }`}
                          style={{ width: `${inst.progress}%` }}
                        />
                      </div>
                    </div>
                  </td>

                  <td className="py-4 px-3">
                    {renderStatusBadge(inst.status)}
                  </td>

                  <td className="py-4 px-3 text-right">
                    <button
                      onClick={() => {
                        loadTemplate(inst.templateKey === 'custom' ? 'onboarding' : inst.templateKey);
                        setCurrentTab('designer');
                      }}
                      className="px-3 py-1.5 rounded-xl border border-[#E2E8F0] dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-100 dark:hover:bg-slate-700 text-[#0F172A] dark:text-slate-200 font-bold text-xs shadow-2xs transition-colors cursor-pointer"
                    >
                      Inspect Graph
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
