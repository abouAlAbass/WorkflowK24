import React, { useState } from 'react';
import { 
  CheckCircle2, 
  Clock, 
  Activity, 
  AlertTriangle, 
  ArrowUpRight, 
  Play, 
  FileText, 
  UserCheck, 
  ShieldAlert, 
  Zap, 
  Terminal, 
  ChevronRight,
  X,
  MessageSquare
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import type { ActionItemTask, InstanceStatus } from '../../types/workflow';

export const HomeDashboard: React.FC = () => {
  const { 
    actionItemTasks, 
    activeInstances, 
    resolveTask, 
    launchQuickWorkflow, 
    setCurrentTab,
    simulationLogs 
  } = useWorkflowStore();

  const [selectedTask, setSelectedTask] = useState<ActionItemTask | null>(null);
  const [modalAction, setModalAction] = useState<'approve' | 'reject' | null>(null);
  const [comment, setComment] = useState('');
  const [taskFilter, setTaskFilter] = useState<'all' | 'pending' | 'high'>('all');

  const pendingCount = actionItemTasks.length;
  const activeCount = activeInstances.filter(i => i.status === 'RUNNING' || i.status === 'PENDING').length;
  const completedTodayCount = activeInstances.filter(i => i.status === 'COMPLETED').length + 14;
  const failedCount = activeInstances.filter(i => i.status === 'FAILED').length;

  const filteredTasks = actionItemTasks.filter(task => {
    if (taskFilter === 'pending') return task.status === 'PENDING';
    if (taskFilter === 'high') return task.priority === 'High';
    return true;
  });

  const handleOpenDecisionModal = (task: ActionItemTask, action: 'approve' | 'reject') => {
    setSelectedTask(task);
    setModalAction(action);
    setComment('');
  };

  const handleConfirmDecision = () => {
    if (selectedTask && modalAction) {
      resolveTask(selectedTask.id, modalAction, comment);
      setSelectedTask(null);
      setModalAction(null);
      setComment('');
    }
  };

  const renderStatusBadge = (status: InstanceStatus) => {
    switch (status) {
      case 'RUNNING':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md text-[11px] font-medium bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-200 dark:border-blue-900/40">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse" />
            RUNNING
          </span>
        );
      case 'COMPLETED':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md text-[11px] font-medium bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-900/40">
            <CheckCircle2 className="w-3 h-3" />
            APPROVED
          </span>
        );
      case 'FAILED':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md text-[11px] font-medium bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-200 dark:border-rose-900/40">
            <AlertTriangle className="w-3 h-3" />
            REJECTED
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md text-[11px] font-medium bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-200 dark:border-amber-900/40">
            <Clock className="w-3 h-3" />
            PENDING
          </span>
        );
    }
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none bg-[#F1F5F9] dark:bg-[#0B0F19]">
      {/* Top Executive Welcome Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 p-6 rounded-2xl bg-gradient-to-r from-slate-900 via-slate-900 to-slate-800 text-white border border-slate-800 shadow-sm">
        <div className="space-y-1">
          <span className="text-[11px] font-mono font-medium tracking-wider uppercase text-slate-400 bg-white/10 px-2.5 py-0.5 rounded-full">
            Executive Control Center
          </span>
          <h2 className="text-xl font-semibold tracking-tight text-white">Welcome back, Mohamed Fetni 👋</h2>
          <p className="text-xs text-slate-300 max-w-xl font-medium leading-relaxed">
            You have <strong className="text-white font-semibold">{pendingCount} pending approvals</strong> requiring decision across {activeCount} active business process workflows.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setCurrentTab('designer')}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer"
          >
            <Play className="w-3.5 h-3.5 fill-white" />
            <span>Open Workflow Canvas</span>
          </button>
        </div>
      </div>

      {/* Top KPI Metrics Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* KPI 1: Pending Approvals */}
        <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-2.5 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-[#475569] dark:text-slate-400">Pending Approvals</span>
            <div className="p-2 rounded-xl bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-200 dark:border-amber-900/40">
              <Clock className="w-4 h-4" />
            </div>
          </div>
          <div>
            <div className="text-2xl font-bold text-[#0F172A] dark:text-white tracking-tight">{pendingCount}</div>
            <div className="flex items-center gap-1 text-[11px] text-emerald-600 dark:text-emerald-400 font-medium mt-0.5">
              <ArrowUpRight className="w-3.5 h-3.5" />
              <span>+12% from last week</span>
            </div>
          </div>
        </div>

        {/* KPI 2: Active Instances */}
        <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-2.5 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-[#475569] dark:text-slate-400">Active Workflow Instances</span>
            <div className="p-2 rounded-xl bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-200 dark:border-blue-900/40">
              <Activity className="w-4 h-4" />
            </div>
          </div>
          <div>
            <div className="text-2xl font-bold text-[#0F172A] dark:text-white tracking-tight">{activeCount}</div>
            <div className="flex items-center gap-1 text-[11px] text-blue-600 dark:text-blue-400 font-medium mt-0.5">
              <span>98.4% SLA compliance</span>
            </div>
          </div>
        </div>

        {/* KPI 3: Completed Today */}
        <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-2.5 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-[#475569] dark:text-slate-400">Completed Today</span>
            <div className="p-2 rounded-xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-900/40">
              <CheckCircle2 className="w-4 h-4" />
            </div>
          </div>
          <div>
            <div className="text-2xl font-bold text-[#0F172A] dark:text-white tracking-tight">{completedTodayCount}</div>
            <div className="flex items-center gap-1 text-[11px] text-slate-500 dark:text-slate-400 font-medium mt-0.5">
              <span>Avg cycle: 4.2 mins</span>
            </div>
          </div>
        </div>

        {/* KPI 4: System Health & Errors */}
        <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-2.5 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-[#475569] dark:text-slate-400">System Health</span>
            <div className="p-2 rounded-xl bg-purple-500/10 text-purple-600 dark:text-purple-400 border border-purple-200 dark:border-purple-900/40">
              <ShieldAlert className="w-4 h-4" />
            </div>
          </div>
          <div>
            <div className="text-2xl font-bold text-[#0F172A] dark:text-white tracking-tight">99.8%</div>
            <div className="flex items-center gap-1 text-[11px] text-amber-600 dark:text-amber-400 font-medium mt-0.5">
              <AlertTriangle className="w-3.5 h-3.5" />
              <span>{failedCount} warning alert</span>
            </div>
          </div>
        </div>
      </div>

      {/* Main Dashboard Workspace Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left 2 Cols: My Action Items Table */}
        <div className="lg:col-span-2 space-y-6">
          {/* My Action Items Card */}
          <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-4 transition-colors">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 className="text-sm font-semibold text-[#0F172A] dark:text-white">
                  My Pending Approvals ({filteredTasks.length})
                </h3>
                <p className="text-xs text-[#475569] dark:text-slate-400 font-medium leading-relaxed">
                  Tasks assigned to you or your permission groups requiring decision
                </p>
              </div>

              {/* Filter Buttons */}
              <div className="flex items-center gap-1 p-1 bg-[#F8FAFC] dark:bg-slate-800/80 rounded-xl border border-[#E2E8F0] dark:border-slate-700">
                <button
                  onClick={() => setTaskFilter('all')}
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                    taskFilter === 'all'
                      ? 'bg-white dark:bg-slate-900 text-[#2563EB] dark:text-blue-400 shadow-2xs'
                      : 'text-[#475569] dark:text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-200'
                  }`}
                >
                  All ({actionItemTasks.length})
                </button>
                <button
                  onClick={() => setTaskFilter('high')}
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                    taskFilter === 'high'
                      ? 'bg-white dark:bg-slate-900 text-rose-600 dark:text-rose-400 shadow-2xs'
                      : 'text-[#475569] dark:text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-200'
                  }`}
                >
                  High Priority
                </button>
              </div>
            </div>

            {/* Tasks Table */}
            {filteredTasks.length === 0 ? (
              <div className="p-8 text-center space-y-2 rounded-xl border border-dashed border-[#E2E8F0] dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/50">
                <CheckCircle2 className="w-8 h-8 text-emerald-500 mx-auto opacity-80" />
                <h4 className="text-xs font-semibold text-[#0F172A] dark:text-white">All tasks completed</h4>
                <p className="text-xs text-[#475569] dark:text-slate-400 font-medium">
                  No pending workflow tasks requiring your approval at this time.
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="border-b border-[#E2E8F0] dark:border-[#1F2937] text-[11px] font-semibold uppercase tracking-wider text-[#475569] dark:text-slate-400">
                      <th className="pb-3 px-2">Task / Workflow</th>
                      <th className="pb-3 px-2">Requester</th>
                      <th className="pb-3 px-2">Due Date</th>
                      <th className="pb-3 px-2">Status</th>
                      <th className="pb-3 px-2 text-right">Quick Decision</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#E2E8F0] dark:divide-[#1F2937] text-xs">
                    {filteredTasks.map((task) => (
                      <tr key={task.id} className="group hover:bg-[#F8FAFC] dark:hover:bg-slate-800/50 transition-colors">
                        <td className="py-3.5 px-2">
                          <div className="font-semibold text-[#0F172A] dark:text-slate-100 group-hover:text-[#2563EB] dark:group-hover:text-blue-400 transition-colors">
                            {task.title}
                          </div>
                          <div className="flex items-center gap-2 text-[11px] text-[#475569] dark:text-slate-400 mt-0.5 font-medium">
                            <span>{task.workflowName}</span>
                            <span>•</span>
                            <span className="font-mono text-[10px] bg-slate-100 dark:bg-slate-800 px-1.5 py-0.2 rounded">
                              {task.id}
                            </span>
                          </div>
                        </td>
                        <td className="py-3.5 px-2">
                          <div className="flex items-center gap-2">
                            <img
                              src={task.requesterAvatar}
                              alt={task.requester}
                              className="w-6 h-6 rounded-full object-cover border border-slate-200 dark:border-slate-700"
                            />
                            <div>
                              <div className="font-semibold text-[#0F172A] dark:text-slate-200">{task.requester}</div>
                              <div className="text-[10px] text-[#475569] dark:text-slate-400 font-medium">{task.department}</div>
                            </div>
                          </div>
                        </td>
                        <td className="py-3.5 px-2 font-medium">
                          <span className="text-[#0F172A] dark:text-slate-200 block font-medium">{task.dueDate}</span>
                          {task.amount && (
                            <span className="text-[10px] font-mono text-[#2563EB] dark:text-blue-400 font-semibold">
                              {task.amount}
                            </span>
                          )}
                        </td>
                        <td className="py-3.5 px-2 font-medium">
                          {renderStatusBadge(task.status)}
                        </td>
                        <td className="py-3.5 px-2 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              onClick={() => handleOpenDecisionModal(task, 'approve')}
                              className="px-3 py-1 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-medium text-xs shadow-2xs transition-colors cursor-pointer"
                            >
                              Approve
                            </button>
                            <button
                              onClick={() => handleOpenDecisionModal(task, 'reject')}
                              className="px-3 py-1 rounded-lg bg-white dark:bg-slate-800 hover:bg-rose-50 dark:hover:bg-rose-950/40 text-rose-600 dark:text-rose-400 border border-slate-200 dark:border-slate-700 font-medium text-xs shadow-2xs transition-colors cursor-pointer"
                            >
                              Reject
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Quick Launch Cards */}
          <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-4 transition-colors">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-semibold text-[#0F172A] dark:text-white flex items-center gap-2">
                  <Zap className="w-4 h-4 text-amber-500 fill-amber-500" />
                  Quick Launch Triggers
                </h3>
                <p className="text-xs text-[#475569] dark:text-slate-400 font-medium leading-relaxed">
                  Initiate standard process instances with 1-click execution
                </p>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div
                onClick={() => launchQuickWorkflow('onboarding')}
                className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/60 hover:border-blue-400 dark:hover:border-blue-500 transition-all cursor-pointer group"
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="p-2 rounded-lg bg-blue-500/10 text-[#2563EB] dark:text-blue-400">
                    <UserCheck className="w-4 h-4" />
                  </div>
                  <ChevronRight className="w-4 h-4 text-slate-400 group-hover:translate-x-0.5 transition-transform" />
                </div>
                <h4 className="text-xs font-semibold text-[#0F172A] dark:text-slate-100">Employee Onboarding</h4>
                <p className="text-[11px] text-[#475569] dark:text-slate-400 mt-1 font-medium leading-relaxed">HR hiring & manager approval</p>
              </div>

              <div
                onClick={() => launchQuickWorkflow('expense')}
                className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/60 hover:border-blue-400 dark:hover:border-blue-500 transition-all cursor-pointer group"
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="p-2 rounded-lg bg-[#2563EB]/10 text-[#2563EB] dark:text-blue-400">
                    <FileText className="w-4 h-4" />
                  </div>
                  <ChevronRight className="w-4 h-4 text-slate-400 group-hover:translate-x-0.5 transition-transform" />
                </div>
                <h4 className="text-xs font-semibold text-[#0F172A] dark:text-slate-100">Purchase Request</h4>
                <p className="text-[11px] text-[#475569] dark:text-slate-400 mt-1 font-medium leading-relaxed">PO sign-off & finance validation</p>
              </div>

              <div
                onClick={() => launchQuickWorkflow('itAccess')}
                className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/60 hover:border-blue-400 dark:hover:border-blue-500 transition-all cursor-pointer group"
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="p-2 rounded-lg bg-[#2563EB]/10 text-[#2563EB] dark:text-blue-400">
                    <ShieldAlert className="w-4 h-4" />
                  </div>
                  <ChevronRight className="w-4 h-4 text-slate-400 group-hover:translate-x-0.5 transition-transform" />
                </div>
                <h4 className="text-xs font-semibold text-[#0F172A] dark:text-slate-100">IT Access Request</h4>
                <p className="text-[11px] text-[#475569] dark:text-slate-400 mt-1 font-medium leading-relaxed">AD permissions & VPN entitlement</p>
              </div>
            </div>
          </div>
        </div>

        {/* Right 1 Col: Recent Execution Audit Feed */}
        <div className="space-y-6">
          <div className="p-5 rounded-2xl bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] shadow-2xs space-y-4 transition-colors">
            <div className="flex items-center justify-between pb-2 border-b border-[#E2E8F0] dark:border-[#1F2937]">
              <div className="flex items-center gap-2">
                <Terminal className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
                <h3 className="text-xs font-semibold text-[#0F172A] dark:text-white">
                  Recent Execution Audit Feed
                </h3>
              </div>
              <span className="text-[10px] font-mono bg-slate-100 dark:bg-slate-800 text-[#475569] dark:text-slate-400 px-2 py-0.5 rounded font-medium">
                Live Feed
              </span>
            </div>

            <div className="space-y-2.5 max-h-[460px] overflow-y-auto pr-1">
              {simulationLogs.length === 0 ? (
                <div className="text-xs text-[#475569] dark:text-slate-400 italic text-center py-6 font-medium">
                  No log transitions recorded yet.
                </div>
              ) : (
                simulationLogs.map((log, idx) => (
                  <div
                    key={idx}
                    className="p-3 rounded-xl border border-slate-200/80 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/60 text-xs space-y-1"
                  >
                    <div className="flex items-center justify-between text-[10px] font-mono">
                      <span className="font-semibold text-[#2563EB] dark:text-blue-400">[{log.nodeName}]</span>
                      <span className="text-slate-400">{log.timestamp}</span>
                    </div>
                    <p className="text-[#0F172A] dark:text-slate-200 font-medium leading-relaxed">
                      {log.message}
                    </p>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Decision Action Modal */}
      {selectedTask && modalAction && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4 select-none animate-fade-in text-[#0F172A] dark:text-slate-100">
          <div className="border border-[#E2E8F0] dark:border-[#1F2937] bg-white dark:bg-[#111827] rounded-2xl w-full max-w-lg shadow-xl overflow-hidden flex flex-col transition-colors">
            {/* Modal Header */}
            <div className="p-4 border-b border-[#E2E8F0] dark:border-[#1F2937] bg-[#F8FAFC] dark:bg-[#0B0F19] flex items-center justify-between">
              <div className="flex items-center gap-2">
                <MessageSquare className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
                <h3 className="font-semibold text-xs text-[#0F172A] dark:text-white">
                  Confirm Decision: {modalAction === 'approve' ? 'Approval' : 'Rejection'}
                </h3>
              </div>
              <button
                onClick={() => {
                  setSelectedTask(null);
                  setModalAction(null);
                }}
                className="p-1 rounded-lg text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-100 transition-colors"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-5 space-y-4">
              <div className="p-3.5 rounded-xl bg-blue-500/10 border border-blue-200 dark:border-blue-900/40 space-y-1">
                <span className="text-[10px] font-medium text-[#475569] dark:text-slate-400 uppercase tracking-wider block">
                  Task Title
                </span>
                <p className="text-xs font-semibold text-[#0F172A] dark:text-white">{selectedTask.title}</p>
                <span className="text-[10px] text-[#2563EB] dark:text-blue-400 block font-mono font-medium">
                  Workflow: {selectedTask.workflowName} ({selectedTask.instanceId})
                </span>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-[#0F172A] dark:text-slate-200 block">
                  Decision Comment / Justification (Optional):
                </label>
                <textarea
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                  placeholder={modalAction === 'approve' ? 'Add optional approval notes...' : 'Reason for rejection...'}
                  rows={3}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-3 text-xs text-[#0F172A] dark:text-slate-100 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                />
              </div>
            </div>

            {/* Modal Footer */}
            <div className="p-4 border-t border-[#E2E8F0] dark:border-[#1F2937] bg-[#F8FAFC] dark:bg-[#0B0F19] flex justify-end gap-2">
              <button
                onClick={() => {
                  setSelectedTask(null);
                  setModalAction(null);
                }}
                className="px-4 py-2 rounded-xl bg-slate-200 dark:bg-slate-800 text-[#0F172A] dark:text-slate-200 hover:bg-slate-300 dark:hover:bg-slate-700 text-xs font-semibold cursor-pointer transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleConfirmDecision}
                className={`px-4 py-2 rounded-xl text-xs font-semibold text-white shadow-2xs cursor-pointer transition-all ${
                  modalAction === 'approve'
                    ? 'bg-emerald-600 hover:bg-emerald-700'
                    : 'bg-rose-600 hover:bg-rose-700'
                }`}
              >
                Confirm {modalAction === 'approve' ? 'Approval' : 'Rejection'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
