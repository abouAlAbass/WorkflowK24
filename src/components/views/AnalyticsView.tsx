import React, { useEffect, useState } from 'react';
import { BarChart3, TrendingUp, ShieldCheck } from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import type { AnalyticsKpiData } from '../../types/workflow';

export const AnalyticsView: React.FC = () => {
  const [kpis, setKpis] = useState<AnalyticsKpiData>({
    avgCompletionTime: '4m 12s',
    slaComplianceRate: '98.6%',
    topBottleneckNode: 'VP Finance Sign-Off',
    topBottleneckQueueTime: '24h 10m',
    totalInstancesProcessed: 1432,
    volumeByDepartment: [
      { department: 'Engineering Onboarding & Access', percentage: 42, instancesCount: 612 },
      { department: 'Finance Purchase Requisitions', percentage: 31, instancesCount: 440 },
      { department: 'IT Security & Cloud Entitlements', percentage: 27, instancesCount: 380 }
    ]
  });

  useEffect(() => {
    let isMounted = true;
    getWorkflowRepository()
      .getAnalyticsKpis()
      .then((data) => {
        if (isMounted && data) {
          setKpis(data);
        }
      })
      .catch((err) => {
        console.warn('Failed to fetch analytics KPIs:', err);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <BarChart3 className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            Process Bottlenecks & Executive SLA Analytics
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Real-time throughput metrics, duration trends, and node bottleneck analysis
          </p>
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-2 transition-colors">
          <span className="text-xs font-bold text-[#64748B] dark:text-slate-400">Avg Completion Time</span>
          <div className="text-2xl font-black text-[#0F172A] dark:text-white">{kpis.avgCompletionTime}</div>
          <span className="text-[11px] text-emerald-600 dark:text-emerald-400 font-bold flex items-center gap-1">
            <TrendingUp className="w-3.5 h-3.5" /> 18% faster than last month
          </span>
        </div>

        <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-2 transition-colors">
          <span className="text-xs font-bold text-[#64748B] dark:text-slate-400">SLA Compliance Rate</span>
          <div className="text-2xl font-black text-[#0F172A] dark:text-white">{kpis.slaComplianceRate}</div>
          <span className="text-[11px] text-blue-600 dark:text-blue-400 font-bold flex items-center gap-1">
            <ShieldCheck className="w-3.5 h-3.5" /> Target: 95.0%
          </span>
        </div>

        <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-2 transition-colors">
          <span className="text-xs font-bold text-[#64748B] dark:text-slate-400">Top Bottleneck Node</span>
          <div className="text-xl font-extrabold text-amber-600 dark:text-amber-400 truncate">{kpis.topBottleneckNode}</div>
          <span className="text-[11px] text-slate-500 dark:text-slate-400 font-medium">
            Avg queue time: {kpis.topBottleneckQueueTime}
          </span>
        </div>
      </div>

      {/* Charts Visualization Placeholder / Breakdown */}
      <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-4 transition-colors">
        <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">
          Workflow Volume Breakdown by Department
        </h3>

        <div className="space-y-3 pt-2">
          {kpis.volumeByDepartment.map((vol, idx) => {
            const colors = ['bg-[#2563EB]', 'bg-purple-600', 'bg-emerald-500', 'bg-amber-500'];
            const barColor = colors[idx % colors.length];
            return (
              <div key={vol.department}>
                <div className="flex justify-between text-xs font-bold text-[#0F172A] dark:text-slate-200 mb-1">
                  <span>{vol.department}</span>
                  <span>{vol.percentage}% ({vol.instancesCount} instances)</span>
                </div>
                <div className="w-full h-3 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                  <div className={`h-full ${barColor} rounded-full`} style={{ width: `${vol.percentage}%` }} />
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
