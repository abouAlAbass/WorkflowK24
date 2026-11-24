import React, { useEffect, useState } from 'react';
import { Settings, Mail, Database, Save, CheckCircle2 } from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import type { SystemSettingsData } from '../../types/workflow';

export const SettingsView: React.FC = () => {
  const [settings, setSettings] = useState<SystemSettingsData>({
    smtpHost: 'smtp.mail.enterprise.com',
    smtpPort: '587',
    smtpSecurity: 'STARTTLS',
    sqlConnectionString: 'Server=sql-prod.internal;Database=K2_WorkflowDB;User Id=k2_admin;Password=********;',
    sqlMaxPoolSize: 100,
    sqlTimeoutSeconds: 30,
    retentionDays: 90,
    telemetryEnabled: true,
    mfaEnforced: false
  });

  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  useEffect(() => {
    let isMounted = true;
    getWorkflowRepository()
      .getSettings()
      .then((data) => {
        if (isMounted && data) {
          setSettings(data);
        }
      })
      .catch((err) => {
        console.warn('Failed to load settings:', err);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const handleSave = async () => {
    setIsSaving(true);
    setSaveSuccess(false);
    try {
      const updated = await getWorkflowRepository().saveSettings(settings);
      setSettings(updated);
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (err) {
      console.error('Failed to save system settings:', err);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Settings className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            General Settings & Connectors Configuration
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Configure SMTP credentials, SQL database connectors, and REST API endpoints
          </p>
        </div>

        <div className="flex items-center gap-2">
          {saveSuccess && (
            <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
              <CheckCircle2 className="w-4 h-4" /> Saved Successfully
            </span>
          )}
          <button
            onClick={handleSave}
            disabled={isSaving}
            className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-md cursor-pointer transition-all"
          >
            <Save className="w-4 h-4" />
            <span>{isSaving ? 'Saving...' : 'Save System Settings'}</span>
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Card 1: SMTP Mail Connector */}
        <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-4 transition-colors">
          <div className="flex items-center gap-2 pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
            <Mail className="w-5 h-5 text-amber-500" />
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">SMTP Email Gateway Config</h3>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">SMTP Host Hostname</label>
              <input
                type="text"
                value={settings.smtpHost}
                onChange={(e) => setSettings({ ...settings, smtpHost: e.target.value })}
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Port</label>
                <input
                  type="text"
                  value={settings.smtpPort}
                  onChange={(e) => setSettings({ ...settings, smtpPort: e.target.value })}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
                />
              </div>

              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">SSL / TLS</label>
                <select
                  value={settings.smtpSecurity}
                  onChange={(e) => setSettings({ ...settings, smtpSecurity: e.target.value })}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-bold"
                >
                  <option value="STARTTLS">STARTTLS (Recommended)</option>
                  <option value="SSL / TLS">SSL / TLS</option>
                  <option value="None">None</option>
                </select>
              </div>
            </div>
          </div>
        </div>

        {/* Card 2: SQL Server & DB Connector */}
        <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-4 transition-colors">
          <div className="flex items-center gap-2 pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
            <Database className="w-5 h-5 text-[#2563EB]" />
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">Enterprise SQL Connector</h3>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">SQL Connection String</label>
              <input
                type="password"
                value={settings.sqlConnectionString}
                onChange={(e) => setSettings({ ...settings, sqlConnectionString: e.target.value })}
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Max Pool Size</label>
                <input
                  type="number"
                  value={settings.sqlMaxPoolSize}
                  onChange={(e) => setSettings({ ...settings, sqlMaxPoolSize: parseInt(e.target.value, 10) || 0 })}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
                />
              </div>

              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Timeout (Sec)</label>
                <input
                  type="number"
                  value={settings.sqlTimeoutSeconds}
                  onChange={(e) => setSettings({ ...settings, sqlTimeoutSeconds: parseInt(e.target.value, 10) || 0 })}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
