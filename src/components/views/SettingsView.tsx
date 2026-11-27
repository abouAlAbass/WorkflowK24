import React, { useEffect, useState } from 'react';
import {
  Settings,
  Mail,
  Database,
  Save,
  CheckCircle2,
  Link2,
  Plus,
  Trash2,
  ShieldCheck,
  KeyRound,
  Activity,
  Users,
  X,
  AlertCircle,
  AlertTriangle,
  RefreshCw,
  Layers,
  History,
  Clock
} from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import { PersonalVaultPanel } from '../modals/PersonalVaultPanel';
import type {
  ConnectionAuthMode,
  ConnectionAuthType,
  ConnectionEnvironmentConfigRecord,
  ConnectionEnvironmentName,
  ConnectionHealthStatus,
  ConnectionPermissionRecord,
  ConnectionRecord,
  ConnectionSecretSource,
  ConnectionServiceType,
  ConnectionTestResult,
  ConnectionUsageResponse,
  SystemSettingsData
} from '../../types/workflow';

const SERVICE_TYPES: Array<{ value: ConnectionServiceType; label: string }> = [
  { value: 'rest', label: 'REST / HTTP' },
  { value: 'postgres', label: 'PostgreSQL (Npgsql)' },
  { value: 'sqlserver', label: 'SQL Server' },
  { value: 'sharepoint', label: 'SharePoint (Microsoft Graph)' }
];

const AUTH_MODES: Array<{ value: ConnectionAuthMode; label: string }> = [
  { value: 'static', label: 'Static Credentials' },
  { value: 'oauth2_client_credentials', label: 'OAuth2 Client Credentials' },
  { value: 'on_behalf_of', label: 'OAuth2 On-Behalf-Of (OBO)' },
  { value: 'user_sso_vault', label: 'User Personal Vault (user_sso_vault)' }
];

const ENVIRONMENTS: ConnectionEnvironmentName[] = ['Dev', 'Test', 'Prod'];

function getSecretExpiryAlert(secretExpiresAt?: string | null): { level: 'expired' | 'warning'; label: string } | null {
  if (!secretExpiresAt) return null;
  const expiresMs = Date.parse(secretExpiresAt);
  if (Number.isNaN(expiresMs)) return null;
  const diffDays = Math.ceil((expiresMs - Date.now()) / (1000 * 60 * 60 * 24));
  if (diffDays <= 0) {
    return { level: 'expired', label: 'Secret expiré' };
  }
  if (diffDays <= 14) {
    return { level: 'warning', label: `Expire dans ${diffDays}j` };
  }
  return null;
}

function getHealthBadgeClasses(status?: ConnectionHealthStatus): string {
  switch (status) {
    case 'Healthy':
      return 'bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800';
    case 'Degraded':
      return 'bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-800';
    case 'Unreachable':
      return 'bg-rose-50 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-800';
    default:
      return 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 border-slate-200 dark:border-slate-700';
  }
}

function formatShortDate(iso?: string | null): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '-';
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
}

const SECRET_SOURCES: Array<{ value: ConnectionSecretSource; label: string }> = [
  { value: 'internal_encrypted', label: 'Internal Encrypted (Data Protection)' },
  { value: 'external_vault', label: 'External Vault (Key)' }
];

const AUTH_TYPES: Array<{ value: ConnectionAuthType; label: string }> = [
  { value: 'none', label: 'None' },
  { value: 'bearer', label: 'Bearer Token' },
  { value: 'basic', label: 'Basic Auth' },
  { value: 'api_key', label: 'API Key' },
  { value: 'oauth2_client_credentials', label: 'OAuth2 Client Credentials' }
];

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

  // External Connections state (secrets are never displayed)
  const [connections, setConnections] = useState<ConnectionRecord[]>([]);
  const [connLoading, setConnLoading] = useState(false);
  const [connError, setConnError] = useState<string | null>(null);
  const [connName, setConnName] = useState('');
  const [connServiceType, setConnServiceType] = useState<ConnectionServiceType>('rest');
  const [connBaseUrl, setConnBaseUrl] = useState('');
  const [connAuthType, setConnAuthType] = useState<ConnectionAuthType>('none');
  const [connAuthMode, setConnAuthMode] = useState<ConnectionAuthMode>('static');
  const [connSecretSource, setConnSecretSource] = useState<ConnectionSecretSource>('internal_encrypted');
  const [connSecretRef, setConnSecretRef] = useState('');
  const [connCredentialsJson, setConnCredentialsJson] = useState('');
  const [connExternalSecretKey, setConnExternalSecretKey] = useState('');
  const [connAllowedHosts, setConnAllowedHosts] = useState('');
  const [connAllowWrite, setConnAllowWrite] = useState(false);
  const [connMaxRows, setConnMaxRows] = useState(200);
  const [isCreatingConn, setIsCreatingConn] = useState(false);

  const availableAuthModes =
    connServiceType === 'sqlserver'
      ? AUTH_MODES.filter((m) => m.value === 'static' || m.value === 'oauth2_client_credentials')
      : connServiceType === 'sharepoint'
        ? AUTH_MODES.filter((m) => m.value === 'static' || m.value === 'oauth2_client_credentials' || m.value === 'on_behalf_of')
        : AUTH_MODES;

  // Connection diagnostics (/test) state
  const [testingConnId, setTestingConnId] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, ConnectionTestResult>>({});

  // Connection permissions panel state
  const [permConn, setPermConn] = useState<ConnectionRecord | null>(null);
  const [permissions, setPermissions] = useState<ConnectionPermissionRecord[]>([]);
  const [permLoading, setPermLoading] = useState(false);
  const [permError, setPermError] = useState<string | null>(null);
  const [permSubjectType, setPermSubjectType] = useState<'User' | 'Role'>('User');
  const [permSubjectId, setPermSubjectId] = useState('');
  const [permCanUse, setPermCanUse] = useState(true);
  const [permCanManage, setPermCanManage] = useState(false);

  // Environments (Dev / Test / Prod) tab state
  const [envConn, setEnvConn] = useState<ConnectionRecord | null>(null);
  const [activeEnvTab, setActiveEnvTab] = useState<ConnectionEnvironmentName>('Dev');
  const [envConfigs, setEnvConfigs] = useState<ConnectionEnvironmentConfigRecord[]>([]);
  const [envLoading, setEnvLoading] = useState(false);
  const [envSaving, setEnvSaving] = useState(false);
  const [envError, setEnvError] = useState<string | null>(null);
  const [envSavedMsg, setEnvSavedMsg] = useState<string | null>(null);
  const [envBaseUrl, setEnvBaseUrl] = useState('');
  const [envCredentialsJson, setEnvCredentialsJson] = useState('');
  const [envExternalKey, setEnvExternalKey] = useState('');

  // Usage Logs & Secret Rotation side drawer state
  const [drawerConn, setDrawerConn] = useState<ConnectionRecord | null>(null);
  const [usageData, setUsageData] = useState<ConnectionUsageResponse | null>(null);
  const [usageLoading, setUsageLoading] = useState(false);
  const [usageError, setUsageError] = useState<string | null>(null);
  const [rotateCredentialsJson, setRotateCredentialsJson] = useState('');
  const [rotateExternalKey, setRotateExternalKey] = useState('');
  const [rotateExpiresAt, setRotateExpiresAt] = useState('');
  const [isRotating, setIsRotating] = useState(false);
  const [rotateSuccess, setRotateSuccess] = useState<string | null>(null);

  const loadConnections = async () => {
    setConnLoading(true);
    setConnError(null);
    try {
      const list = await getWorkflowRepository().getConnections();
      setConnections(list);
    } catch (err: any) {
      setConnError(err?.message || 'Unable to load connections.');
    } finally {
      setConnLoading(false);
    }
  };

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

    loadConnections();

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

  const handleCreateConnection = async (e: React.FormEvent) => {
    e.preventDefault();
    setConnError(null);

    const trimmedName = connName.trim();
    const trimmedUrl = connBaseUrl.trim();
    if (!trimmedName) {
      setConnError('Connection name is required.');
      return;
    }

    if ((connServiceType === 'rest' || connServiceType === 'sharepoint') && !trimmedUrl) {
      setConnError(
        connServiceType === 'sharepoint'
          ? 'Base URL is required for SharePoint Graph connections (e.g. https://graph.microsoft.com/v1.0/sites/{siteId}).'
          : 'Base URL is required for REST connections.'
      );
      return;
    }

    const boundedMaxRows = Math.max(1, Math.min(1000, Number(connMaxRows) || 200));

    const hosts = connAllowedHosts
      .split(',')
      .map((h) => h.trim())
      .filter(Boolean);

    if ((connServiceType === 'rest' || connServiceType === 'sharepoint') && hosts.length === 0) {
      try {
        hosts.push(new URL(trimmedUrl).hostname);
      } catch {
        setConnError('Base URL must be a valid HTTP or HTTPS URL.');
        return;
      }
    }

    setIsCreatingConn(true);
    try {
      const created = await getWorkflowRepository().createConnection({
        name: trimmedName,
        serviceType: connServiceType,
        baseUrl: trimmedUrl,
        authType: connAuthType,
        authMode: connAuthMode,
        secretSource: connSecretSource,
        secretRef: connSecretRef.trim() || null,
        credentialsJson: connAuthMode !== 'user_sso_vault' && connSecretSource === 'internal_encrypted' && connCredentialsJson.trim()
          ? connCredentialsJson.trim()
          : null,
        externalSecretKey: connSecretSource === 'external_vault' && connExternalSecretKey.trim()
          ? connExternalSecretKey.trim()
          : null,
        allowedHosts: hosts,
        allowWrite: connAllowWrite,
        maxRows: boundedMaxRows
      });
      setConnections((prev) => [created, ...prev]);
      setConnName('');
      setConnBaseUrl('');
      setConnAuthType('none');
      setConnAuthMode('static');
      setConnSecretSource('internal_encrypted');
      setConnSecretRef('');
      setConnCredentialsJson('');
      setConnExternalSecretKey('');
      setConnAllowedHosts('');
      setConnAllowWrite(false);
      setConnMaxRows(200);
    } catch (err: any) {
      setConnError(err?.message || 'Failed to create connection.');
    } finally {
      setIsCreatingConn(false);
    }
  };

  const handleDeleteConnection = async (id: string) => {
    setConnError(null);
    try {
      await getWorkflowRepository().deleteConnection(id);
      setConnections((prev) => prev.filter((c) => c.id !== id));
      if (permConn?.id === id) {
        setPermConn(null);
      }
    } catch (err: any) {
      setConnError(err?.message || 'Failed to delete connection.');
    }
  };

  const handleTestConnection = async (id: string) => {
    setTestingConnId(id);
    setConnError(null);
    try {
      const result = await getWorkflowRepository().testConnection(id);
      setTestResults((prev) => ({ ...prev, [id]: result }));
      await loadConnections();
    } catch (err: any) {
      setTestResults((prev) => ({
        ...prev,
        [id]: { success: false, latencyMs: 0, errorMessage: err?.message || 'Test failed.' }
      }));
    } finally {
      setTestingConnId(null);
    }
  };

  const handleOpenPermissions = async (conn: ConnectionRecord) => {
    setPermConn(conn);
    setPermError(null);
    setPermLoading(true);
    try {
      const list = await getWorkflowRepository().getConnectionPermissions(conn.id);
      setPermissions(list);
    } catch (err: any) {
      setPermError(err?.message || 'Unable to load permissions.');
      setPermissions([]);
    } finally {
      setPermLoading(false);
    }
  };

  const handleUpsertPermission = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!permConn || !permSubjectId.trim()) return;
    setPermError(null);
    try {
      await getWorkflowRepository().upsertConnectionPermission(permConn.id, {
        subjectType: permSubjectType,
        subjectId: permSubjectId.trim(),
        canUse: permCanUse || permCanManage,
        canManage: permCanManage
      });
      setPermSubjectId('');
      const refreshed = await getWorkflowRepository().getConnectionPermissions(permConn.id);
      setPermissions(refreshed);
    } catch (err: any) {
      setPermError(err?.message || 'Failed to save permission.');
    }
  };

  const handleDeletePermission = async (permissionId: string) => {
    if (!permConn) return;
    setPermError(null);
    try {
      await getWorkflowRepository().deleteConnectionPermission(permConn.id, permissionId);
      setPermissions((prev) => prev.filter((p) => p.id !== permissionId));
    } catch (err: any) {
      setPermError(err?.message || 'Failed to revoke permission.');
    }
  };

  const syncEnvFormFields = (configs: ConnectionEnvironmentConfigRecord[], envName: ConnectionEnvironmentName) => {
    const match = configs.find((c) => c.environmentName === envName);
    setEnvBaseUrl(match?.baseUrlOverride ?? '');
    setEnvExternalKey(match?.externalSecretKeyOverride ?? '');
    setEnvCredentialsJson('');
  };

  const handleOpenEnvironments = async (conn: ConnectionRecord) => {
    setEnvConn(conn);
    setActiveEnvTab('Dev');
    setEnvError(null);
    setEnvSavedMsg(null);
    setEnvLoading(true);
    try {
      const list = await getWorkflowRepository().getConnectionEnvironments(conn.id);
      setEnvConfigs(list);
      syncEnvFormFields(list, 'Dev');
    } catch (err: any) {
      setEnvError(err?.message || 'Impossible de charger les environnements.');
      setEnvConfigs([]);
      syncEnvFormFields([], 'Dev');
    } finally {
      setEnvLoading(false);
    }
  };

  const handleSelectEnvTab = (envName: ConnectionEnvironmentName) => {
    setActiveEnvTab(envName);
    setEnvSavedMsg(null);
    setEnvError(null);
    syncEnvFormFields(envConfigs, envName);
  };

  const handleSaveEnvironment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!envConn) return;
    setEnvSaving(true);
    setEnvError(null);
    setEnvSavedMsg(null);
    try {
      const saved = await getWorkflowRepository().upsertConnectionEnvironment(envConn.id, {
        environmentName: activeEnvTab,
        baseUrlOverride: envBaseUrl.trim() || null,
        credentialsJsonOverride: envCredentialsJson.trim() || null,
        externalSecretKeyOverride: envExternalKey.trim() || null
      });
      const next = [...envConfigs.filter((c) => c.environmentName !== activeEnvTab), saved];
      setEnvConfigs(next);
      setEnvCredentialsJson('');
      setEnvSavedMsg(`Configuration ${activeEnvTab} enregistrée.`);
    } catch (err: any) {
      setEnvError(err?.message || 'Échec de sauvegarde de la configuration environnement.');
    } finally {
      setEnvSaving(false);
    }
  };

  const handleOpenUsageDrawer = async (conn: ConnectionRecord) => {
    setDrawerConn(conn);
    setUsageError(null);
    setRotateSuccess(null);
    setRotateCredentialsJson('');
    setRotateExternalKey(conn.externalSecretKey || '');
    setRotateExpiresAt(conn.secretExpiresAt ? conn.secretExpiresAt.slice(0, 10) : '');
    setUsageLoading(true);
    try {
      const data = await getWorkflowRepository().getConnectionUsage(conn.id, 50);
      setUsageData(data);
    } catch (err: any) {
      setUsageError(err?.message || "Impossible de charger le journal d'usage.");
      setUsageData(null);
    } finally {
      setUsageLoading(false);
    }
  };

  const handleRotateSecret = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!drawerConn) return;
    if (!rotateCredentialsJson.trim() && !rotateExternalKey.trim()) {
      setUsageError('Fournissez un nouveau Credentials JSON ou une clé External Vault pour effectuer la rotation.');
      return;
    }
    setIsRotating(true);
    setUsageError(null);
    setRotateSuccess(null);
    try {
      const updated = await getWorkflowRepository().rotateConnectionSecret(drawerConn.id, {
        credentialsJson: rotateCredentialsJson.trim() || null,
        externalSecretKey: rotateExternalKey.trim() || null,
        secretExpiresAt: rotateExpiresAt ? new Date(`${rotateExpiresAt}T23:59:59Z`).toISOString() : null,
        expectedVersion: drawerConn.version
      });
      setConnections((prev) => prev.map((c) => (c.id === updated.id ? updated : c)));
      setDrawerConn(updated);
      setRotateCredentialsJson('');
      setRotateSuccess('Rotation du secret effectuée et auditée avec succès.');
      const refreshedUsage = await getWorkflowRepository().getConnectionUsage(updated.id, 50);
      setUsageData(refreshedUsage);
    } catch (err: any) {
      setUsageError(err?.message || 'Échec de la rotation du secret.');
    } finally {
      setIsRotating(false);
    }
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none text-start">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Settings className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            General Settings &amp; Connectors Configuration
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Configure service brokers (REST, PostgreSQL), fine-grained connection permissions, and your personal credential vault
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

      {/* External Service Connections Manager */}
      <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-5 transition-colors">
        <div className="flex items-center justify-between pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
          <div className="flex items-center gap-2">
            <Link2 className="w-5 h-5 text-[#0891B2]" />
            <div>
              <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">
                Gestionnaire de Connexions (REST, PostgreSQL, SQL Server &amp; SharePoint)
              </h3>
              <p className="text-[11px] text-[#64748B] dark:text-slate-400">
                Seules les connexions autorisées pour votre rôle sont affichées. Les secrets sont résolus uniquement à l&apos;exécution.
              </p>
            </div>
          </div>
          <span className="px-2.5 py-1 rounded-full bg-cyan-50 dark:bg-cyan-950/50 text-[#0891B2] dark:text-cyan-300 border border-cyan-200 dark:border-cyan-800 text-[11px] font-bold flex items-center gap-1">
            <ShieldCheck className="w-3.5 h-3.5" /> Gouvernance &amp; SSRF
          </span>
        </div>

        {connError && (
          <div role="alert" className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-xs font-semibold text-rose-700 dark:text-rose-300">
            {connError}
          </div>
        )}

        <form onSubmit={handleCreateConnection} className="grid grid-cols-1 md:grid-cols-4 gap-3 items-end text-xs">
          <div>
            <label htmlFor="conn-name-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Name
            </label>
            <input
              id="conn-name-input"
              type="text"
              required
              value={connName}
              onChange={(e) => setConnName(e.target.value)}
              placeholder="ERP_PROD_API"
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
            />
          </div>

          <div>
            <label htmlFor="conn-servicetype-select" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Service Broker
            </label>
            <select
              id="conn-servicetype-select"
              value={connServiceType}
              onChange={(e) => {
                const nextType = e.target.value as ConnectionServiceType;
                setConnServiceType(nextType);
                if ((nextType === 'sqlserver' || nextType === 'sharepoint') && connAuthMode === 'user_sso_vault') {
                  setConnAuthMode('static');
                }
                if (nextType === 'sharepoint') {
                  setConnAuthMode('oauth2_client_credentials');
                  setConnAuthType('oauth2_client_credentials');
                }
              }}
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-bold text-[#0F172A] dark:text-slate-100"
            >
              {SERVICE_TYPES.map((s) => (
                <option key={s.value} value={s.value}>{s.label}</option>
              ))}
            </select>
          </div>

          <div className="md:col-span-2">
            <label htmlFor="conn-baseurl-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              {connServiceType === 'postgres'
                ? 'Endpoint (postgres://host:5432/db)'
                : connServiceType === 'sqlserver'
                  ? 'Endpoint (sqlserver://host:1433/db)'
                  : connServiceType === 'sharepoint'
                    ? 'Graph Site Base URL (https://graph.microsoft.com/v1.0/sites/{siteId})'
                    : 'Base URL'}
            </label>
            <input
              id="conn-baseurl-input"
              type={connServiceType === 'rest' || connServiceType === 'sharepoint' ? 'url' : 'text'}
              required={connServiceType === 'rest' || connServiceType === 'sharepoint'}
              value={connBaseUrl}
              onChange={(e) => setConnBaseUrl(e.target.value)}
              placeholder={
                connServiceType === 'postgres'
                  ? 'postgres://db.internal:5432/appdb'
                  : connServiceType === 'sqlserver'
                    ? 'sqlserver://sql-prod.internal:1433/ERP_DB'
                    : connServiceType === 'sharepoint'
                      ? 'https://graph.microsoft.com/v1.0/sites/contoso.sharepoint.com,site-id'
                      : 'https://api.partner.com'
              }
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
            />
          </div>

          <div>
            <label htmlFor="conn-authmode-select" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Auth Mode
            </label>
            <select
              id="conn-authmode-select"
              value={connAuthMode}
              onChange={(e) => setConnAuthMode(e.target.value as ConnectionAuthMode)}
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-bold text-[#0F172A] dark:text-slate-100"
            >
              {availableAuthModes.map((m) => (
                <option key={m.value} value={m.value}>{m.label}</option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="conn-authtype-select" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Auth Type
            </label>
            <select
              id="conn-authtype-select"
              value={connAuthType}
              onChange={(e) => setConnAuthType(e.target.value as ConnectionAuthType)}
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-bold text-[#0F172A] dark:text-slate-100"
            >
              {AUTH_TYPES.map((a) => (
                <option key={a.value} value={a.value}>{a.label}</option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="conn-secretsource-select" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Secret Source
            </label>
            <select
              id="conn-secretsource-select"
              disabled={connAuthMode === 'user_sso_vault'}
              value={connSecretSource}
              onChange={(e) => setConnSecretSource(e.target.value as ConnectionSecretSource)}
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-bold text-[#0F172A] dark:text-slate-100 disabled:opacity-50"
            >
              {SECRET_SOURCES.map((s) => (
                <option key={s.value} value={s.value}>{s.label}</option>
              ))}
            </select>
          </div>

          {connSecretSource === 'external_vault' ? (
            <div>
              <label htmlFor="conn-extkey-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                External Vault Key
              </label>
              <input
                id="conn-extkey-input"
                type="text"
                value={connExternalSecretKey}
                onChange={(e) => setConnExternalSecretKey(e.target.value)}
                placeholder="PG_PROD_SECRET"
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
              />
            </div>
          ) : (
            <div>
              <label htmlFor="conn-creds-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                Credentials JSON (chiffré)
              </label>
              <input
                id="conn-creds-input"
                type="password"
                disabled={connAuthMode === 'user_sso_vault'}
                value={connCredentialsJson}
                onChange={(e) => setConnCredentialsJson(e.target.value)}
                placeholder='{"token":"..."} ou {"username":"...","password":"..."}'
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100 disabled:opacity-50"
              />
            </div>
          )}

          <div>
            <label htmlFor="conn-secretref-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Legacy Secret Ref (optionnel)
            </label>
            <input
              id="conn-secretref-input"
              type="text"
              value={connSecretRef}
              onChange={(e) => setConnSecretRef(e.target.value)}
              placeholder="env:ERP_API_TOKEN"
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
            />
          </div>

          <div className="md:col-span-2">
            <label htmlFor="conn-hosts-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Allowed Hosts (REST / SharePoint SSRF)
            </label>
            <input
              id="conn-hosts-input"
              type="text"
              value={connAllowedHosts}
              onChange={(e) => setConnAllowedHosts(e.target.value)}
              placeholder={connServiceType === 'sharepoint' ? 'graph.microsoft.com' : 'api.partner.com'}
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
            />
          </div>

          <div>
            <label htmlFor="conn-maxrows-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
              Nombre max de lignes (MaxRows, max 1000)
            </label>
            <input
              id="conn-maxrows-input"
              type="number"
              min={1}
              max={1000}
              value={connMaxRows}
              onChange={(e) => setConnMaxRows(Math.max(1, Math.min(1000, Number(e.target.value) || 1)))}
              className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
            />
          </div>

          <div className="md:col-span-2 flex items-center gap-2 pb-1">
            <input
              id="conn-allowwrite-checkbox"
              type="checkbox"
              checked={connAllowWrite}
              onChange={(e) => setConnAllowWrite(e.target.checked)}
              className="h-4 w-4 rounded border-slate-300 text-[#0891B2] focus:ring-[#0891B2]"
            />
            <label htmlFor="conn-allowwrite-checkbox" className="font-bold text-[#0F172A] dark:text-slate-200 cursor-pointer">
              Autoriser les opérations d&apos;écriture (AllowWrite — lecture seule par défaut)
            </label>
          </div>

          <div className="flex justify-end">
            <button
              type="submit"
              disabled={isCreatingConn}
              className="w-full px-3 py-2 bg-[#0891B2] hover:bg-cyan-700 disabled:opacity-50 text-white rounded-xl font-bold flex items-center justify-center gap-1 cursor-pointer"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Ajouter</span>
            </button>
          </div>

          <div
            role="note"
            className="md:col-span-4 p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/70 text-[11px] text-amber-900 dark:text-amber-200 flex items-start gap-2"
          >
            <ShieldCheck className="w-4 h-4 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
            <div>
              <strong className="font-extrabold">Recommandation sécurité (privilèges minimaux) :</strong>{' '}
              Pour <strong>SQL Server</strong>, utilisez un compte SQL dédié à privilèges minimaux (droits{' '}
              <code className="font-mono font-bold">SELECT</code> uniquement en mode lecture seule). Pour{' '}
              <strong>SharePoint / Microsoft Graph</strong>, privilégiez la permission Entra ID{' '}
              <code className="font-mono font-bold">Sites.Selected</code> plutôt qu&apos;un accès global au tenant.
            </div>
          </div>
        </form>

        <div className="overflow-x-auto">
          {connLoading ? (
            <p className="text-xs text-[#64748B] dark:text-slate-400 py-3">Loading connections...</p>
          ) : connections.length === 0 ? (
            <p className="text-xs text-[#64748B] dark:text-slate-400 py-3">No external connections configured yet.</p>
          ) : (
            <table className="w-full text-xs text-start border-collapse">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-700 text-[#64748B] dark:text-slate-400 uppercase text-[10px]">
                  <th className="py-2 pe-3 text-start">Name</th>
                  <th className="py-2 pe-3 text-start">Broker</th>
                  <th className="py-2 pe-3 text-start">Endpoint</th>
                  <th className="py-2 pe-3 text-start">Auth / Mode</th>
                  <th className="py-2 pe-3 text-start">Santé &amp; Activité</th>
                  <th className="py-2 text-end">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {connections.map((conn) => {
                  const testRes = testResults[conn.id];
                  const expiryAlert = getSecretExpiryAlert(conn.secretExpiresAt);
                  const healthStatus: ConnectionHealthStatus = conn.healthStatus || 'Unknown';
                  return (
                    <tr key={conn.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/50">
                      <td className="py-2.5 pe-3 font-mono font-bold text-[#0F172A] dark:text-slate-100">
                        <div className="flex flex-col gap-1">
                          <span>{conn.name}</span>
                          {expiryAlert && (
                            <span
                              role="status"
                              className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold w-fit ${
                                expiryAlert.level === 'expired'
                                  ? 'bg-rose-100 dark:bg-rose-950/70 text-rose-700 dark:text-rose-300 border border-rose-300 dark:border-rose-800'
                                  : 'bg-amber-100 dark:bg-amber-950/70 text-amber-800 dark:text-amber-300 border border-amber-300 dark:border-amber-800'
                              }`}
                            >
                              <AlertTriangle className="w-3 h-3 shrink-0" />
                              {expiryAlert.label}
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="py-2.5 pe-3">
                        <span className="px-2 py-0.5 rounded-md bg-cyan-50 dark:bg-cyan-950/60 text-cyan-700 dark:text-cyan-300 font-mono font-semibold text-[11px]">
                          {conn.serviceType || 'rest'}
                        </span>
                      </td>
                      <td className="py-2.5 pe-3 font-mono text-[#475569] dark:text-slate-300">
                        {conn.baseUrl || '-'}
                      </td>
                      <td className="py-2.5 pe-3">
                        <div className="flex flex-wrap items-center gap-1">
                          <span className="px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 font-mono font-semibold text-[11px]">
                            {conn.authMode || 'static'}
                          </span>
                          <span className="px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 font-mono text-[10px] text-slate-500">
                            {conn.authType}
                          </span>
                          <span
                            className={`px-2 py-0.5 rounded-md font-mono text-[10px] font-bold ${
                              conn.allowWrite
                                ? 'bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300'
                                : 'bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300'
                            }`}
                          >
                            {conn.allowWrite ? 'R/W' : 'RO'} · max {conn.maxRows ?? 200}
                          </span>
                          {(conn.hasEncryptedCredentials || conn.hasSecretRef) && (
                            <KeyRound className="w-3 h-3 text-emerald-600 dark:text-emerald-400" />
                          )}
                        </div>
                      </td>
                      <td className="py-2.5 pe-3">
                        <div className="flex flex-col gap-1">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <span
                              className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full border text-[10px] font-bold ${getHealthBadgeClasses(healthStatus)}`}
                            >
                              {healthStatus}
                            </span>
                            {testRes && (
                              testRes.success ? (
                                <span className="inline-flex items-center gap-1 text-emerald-700 dark:text-emerald-400 font-semibold text-[10px]">
                                  <CheckCircle2 className="w-3 h-3" /> {testRes.latencyMs} ms
                                </span>
                              ) : (
                                <span className="inline-flex items-center gap-1 text-rose-600 dark:text-rose-400 font-semibold text-[10px]" title={testRes.errorMessage || ''}>
                                  <AlertCircle className="w-3 h-3 shrink-0" /> {testRes.errorMessage || 'Erreur'}
                                </span>
                              )
                            )}
                          </div>
                          <div className="text-[10px] text-slate-500 dark:text-slate-400 flex items-center gap-2">
                            <span title="Dernier test">Test: {formatShortDate(conn.lastTestedAt)}</span>
                            <span title="Dernier usage">Usage: {formatShortDate(conn.lastUsedAt)}</span>
                          </div>
                        </div>
                      </td>
                      <td className="py-2.5 text-end">
                        <div className="inline-flex items-center gap-1 flex-wrap justify-end">
                          <button
                            type="button"
                            disabled={testingConnId === conn.id}
                            onClick={() => handleTestConnection(conn.id)}
                            className="px-2.5 py-1 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 font-bold text-[11px] inline-flex items-center gap-1 cursor-pointer disabled:opacity-50"
                          >
                            <Activity className="w-3 h-3" />
                            <span>{testingConnId === conn.id ? 'Test...' : 'Tester'}</span>
                          </button>

                          {conn.canManage !== false && (
                            <>
                              <button
                                type="button"
                                onClick={() => handleOpenEnvironments(conn)}
                                className="px-2.5 py-1 rounded-lg bg-indigo-50 dark:bg-indigo-950/50 text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100 font-bold text-[11px] inline-flex items-center gap-1 cursor-pointer"
                              >
                                <Layers className="w-3 h-3" />
                                <span>Environnements</span>
                              </button>

                              <button
                                type="button"
                                onClick={() => handleOpenUsageDrawer(conn)}
                                className="px-2.5 py-1 rounded-lg bg-amber-50 dark:bg-amber-950/50 text-amber-700 dark:text-amber-300 hover:bg-amber-100 font-bold text-[11px] inline-flex items-center gap-1 cursor-pointer"
                              >
                                <History className="w-3 h-3" />
                                <span>Usage &amp; Rotation</span>
                              </button>

                              <button
                                type="button"
                                onClick={() => handleOpenPermissions(conn)}
                                className="px-2.5 py-1 rounded-lg bg-blue-50 dark:bg-blue-950/50 text-blue-700 dark:text-blue-300 hover:bg-blue-100 font-bold text-[11px] inline-flex items-center gap-1 cursor-pointer"
                              >
                                <Users className="w-3 h-3" />
                                <span>Permissions</span>
                              </button>

                              <button
                                type="button"
                                onClick={() => handleDeleteConnection(conn.id)}
                                className="p-1.5 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40 cursor-pointer"
                                aria-label={`Delete connection ${conn.name}`}
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Permissions Panel */}
        {permConn && (
          <div className="p-4 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/70 border border-blue-200 dark:border-blue-800 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Users className="w-4 h-4 text-[#2563EB]" />
                <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white">
                  Permissions pour <code className="font-mono">{permConn.name}</code>
                </h4>
              </div>
              <button
                type="button"
                onClick={() => setPermConn(null)}
                aria-label="Fermer le panneau des permissions"
                className="p-1 rounded-lg text-slate-400 hover:text-slate-700 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {permError && (
              <div role="alert" className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-xs text-rose-700 dark:text-rose-300">
                {permError}
              </div>
            )}

            <form onSubmit={handleUpsertPermission} className="grid grid-cols-1 md:grid-cols-5 gap-2 items-end text-xs">
              <div>
                <label htmlFor="perm-subject-type" className="font-bold block mb-1">Type</label>
                <select
                  id="perm-subject-type"
                  value={permSubjectType}
                  onChange={(e) => setPermSubjectType(e.target.value as 'User' | 'Role')}
                  className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-bold"
                >
                  <option value="User">User</option>
                  <option value="Role">Role</option>
                </select>
              </div>
              <div className="md:col-span-2">
                <label htmlFor="perm-subject-id" className="font-bold block mb-1">
                  {permSubjectType === 'User' ? 'Email / User ID' : 'Nom du rôle'}
                </label>
                <input
                  id="perm-subject-id"
                  type="text"
                  required
                  value={permSubjectId}
                  onChange={(e) => setPermSubjectId(e.target.value)}
                  placeholder={permSubjectType === 'User' ? 'designer@tamam.local' : 'Designer'}
                  className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                />
              </div>
              <div className="flex items-center gap-3 pb-2">
                <label htmlFor="perm-can-use" className="inline-flex items-center gap-1.5 font-semibold cursor-pointer">
                  <input
                    id="perm-can-use"
                    type="checkbox"
                    checked={permCanUse || permCanManage}
                    onChange={(e) => setPermCanUse(e.target.checked)}
                  />
                  <span>CanUse</span>
                </label>
                <label htmlFor="perm-can-manage" className="inline-flex items-center gap-1.5 font-semibold cursor-pointer">
                  <input
                    id="perm-can-manage"
                    type="checkbox"
                    checked={permCanManage}
                    onChange={(e) => setPermCanManage(e.target.checked)}
                  />
                  <span>CanManage</span>
                </label>
              </div>
              <button
                type="submit"
                className="px-3 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl font-bold cursor-pointer"
              >
                Enregistrer
              </button>
            </form>

            {permLoading ? (
              <p className="text-xs text-slate-400">Chargement des permissions...</p>
            ) : permissions.length === 0 ? (
              <p className="text-xs text-slate-400">Aucune règle explicite (seuls les Admins et le créateur y ont accès).</p>
            ) : (
              <ul className="space-y-1.5 text-xs">
                {permissions.map((p) => (
                  <li
                    key={p.id}
                    className="flex items-center justify-between p-2.5 rounded-xl bg-white dark:bg-slate-800 border border-[#E2E8F0] dark:border-slate-700"
                  >
                    <div className="flex items-center gap-2">
                      <span className="px-2 py-0.5 rounded bg-slate-100 dark:bg-slate-700 font-mono text-[10px] font-bold">
                        {p.subjectType}
                      </span>
                      <span className="font-mono font-semibold">{p.subjectId}</span>
                      {p.canUse && (
                        <span className="px-2 py-0.5 rounded-full bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-[10px] font-bold">
                          CanUse
                        </span>
                      )}
                      {p.canManage && (
                        <span className="px-2 py-0.5 rounded-full bg-blue-50 dark:bg-blue-950/60 text-blue-700 dark:text-blue-300 text-[10px] font-bold">
                          CanManage
                        </span>
                      )}
                    </div>
                    <button
                      type="button"
                      onClick={() => handleDeletePermission(p.id)}
                      aria-label={`Supprimer permission ${p.subjectId}`}
                      className="p-1 rounded-lg text-slate-400 hover:text-rose-600 cursor-pointer"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {/* Environments (Dev / Test / Prod) Override Panel */}
        {envConn && (
          <div className="p-4 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/70 border border-indigo-200 dark:border-indigo-800 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Layers className="w-4 h-4 text-indigo-600 dark:text-indigo-400" />
                <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white">
                  Environnements (Dev / Test / Prod) — <code className="font-mono">{envConn.name}</code>
                </h4>
              </div>
              <button
                type="button"
                onClick={() => setEnvConn(null)}
                aria-label="Fermer le panneau des environnements"
                className="p-1 rounded-lg text-slate-400 hover:text-slate-700 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="flex items-center gap-2" role="tablist" aria-label="Environnements cible">
              {ENVIRONMENTS.map((envName) => {
                const hasConfig = envConfigs.some((c) => c.environmentName === envName);
                const isActive = activeEnvTab === envName;
                return (
                  <button
                    key={envName}
                    type="button"
                    role="tab"
                    aria-selected={isActive}
                    onClick={() => handleSelectEnvTab(envName)}
                    className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-colors cursor-pointer ${
                      isActive
                        ? 'bg-indigo-600 text-white shadow-xs'
                        : 'bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 border border-[#E2E8F0] dark:border-slate-700 hover:bg-indigo-50 dark:hover:bg-indigo-950/40'
                    }`}
                  >
                    {envName} {hasConfig ? '•' : ''}
                  </button>
                );
              })}
            </div>

            {envError && (
              <div role="alert" className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-xs text-rose-700 dark:text-rose-300">
                {envError}
              </div>
            )}
            {envSavedMsg && (
              <div role="status" className="p-2.5 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-xs text-emerald-700 dark:text-emerald-300 font-semibold">
                {envSavedMsg}
              </div>
            )}

            {envLoading ? (
              <p className="text-xs text-slate-400">Chargement des configurations...</p>
            ) : (
              <form onSubmit={handleSaveEnvironment} className="grid grid-cols-1 md:grid-cols-3 gap-3 items-end text-xs">
                <div>
                  <label htmlFor="env-baseurl-input" className="font-bold block mb-1">
                    URL cible ({activeEnvTab})
                  </label>
                  <input
                    id="env-baseurl-input"
                    type="text"
                    value={envBaseUrl}
                    onChange={(e) => setEnvBaseUrl(e.target.value)}
                    placeholder={envConn.baseUrl || 'https://prod.api.enterprise.com'}
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                  />
                </div>
                <div>
                  <label htmlFor="env-creds-input" className="font-bold block mb-1">
                    Credentials JSON ({activeEnvTab}, chiffré)
                  </label>
                  <input
                    id="env-creds-input"
                    type="password"
                    value={envCredentialsJson}
                    onChange={(e) => setEnvCredentialsJson(e.target.value)}
                    placeholder={
                      envConfigs.find((c) => c.environmentName === activeEnvTab)?.hasEncryptedCredentialsOverride
                        ? '•••••••• (laisser vide pour conserver)'
                        : '{"token":"..."}'
                    }
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                  />
                </div>
                <div>
                  <label htmlFor="env-extkey-input" className="font-bold block mb-1">
                    External Vault Key ({activeEnvTab}, optionnel)
                  </label>
                  <input
                    id="env-extkey-input"
                    type="text"
                    value={envExternalKey}
                    onChange={(e) => setEnvExternalKey(e.target.value)}
                    placeholder={`SECRET_${activeEnvTab.toUpperCase()}`}
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                  />
                </div>
                <div className="md:col-span-3 flex justify-end">
                  <button
                    type="submit"
                    disabled={envSaving}
                    className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-xl font-bold cursor-pointer"
                  >
                    {envSaving ? 'Enregistrement...' : `Enregistrer surcharge ${activeEnvTab}`}
                  </button>
                </div>
              </form>
            )}
          </div>
        )}
      </div>

      {/* Side Drawer: Journal d'usage & Rotation du secret */}
      {drawerConn && (
        <div className="fixed inset-0 z-50 flex justify-end bg-slate-900/40 backdrop-blur-[1px]">
          <aside
            aria-label={`Journal d'usage et rotation pour ${drawerConn.name}`}
            className="w-full max-w-lg bg-white dark:bg-[#1E293B] h-full shadow-2xl border-s border-[#E2E8F0] dark:border-[#334155] flex flex-col overflow-hidden"
          >
            <div className="p-5 border-b border-[#E2E8F0] dark:border-[#334155] flex items-center justify-between">
              <div className="flex items-center gap-2">
                <History className="w-5 h-5 text-amber-600 dark:text-amber-400" />
                <div>
                  <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">
                    Journal d&apos;usage &amp; Rotation
                  </h3>
                  <p className="text-[11px] font-mono text-[#64748B] dark:text-slate-400">{drawerConn.name}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setDrawerConn(null)}
                aria-label="Fermer le tiroir d'usage et rotation"
                className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-5 space-y-5 text-xs">
              {usageError && (
                <div role="alert" className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-rose-700 dark:text-rose-300 font-semibold">
                  {usageError}
                </div>
              )}
              {rotateSuccess && (
                <div role="status" className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-emerald-700 dark:text-emerald-300 font-semibold">
                  {rotateSuccess}
                </div>
              )}

              {/* Health & Lifecycle Summary */}
              <div className="p-4 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/70 border border-[#E2E8F0] dark:border-slate-700 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="font-bold text-[#0F172A] dark:text-white">État de santé</span>
                  <span
                    className={`px-2.5 py-0.5 rounded-full border text-[11px] font-bold ${getHealthBadgeClasses(
                      usageData?.healthStatus || drawerConn.healthStatus || 'Unknown'
                    )}`}
                  >
                    {usageData?.healthStatus || drawerConn.healthStatus || 'Unknown'}
                  </span>
                </div>
                <div className="grid grid-cols-2 gap-2 text-[11px] text-slate-600 dark:text-slate-300">
                  <div>Dernier test : <span className="font-mono">{formatShortDate(usageData?.lastTestedAt ?? drawerConn.lastTestedAt)}</span></div>
                  <div>Dernier usage : <span className="font-mono">{formatShortDate(usageData?.lastUsedAt ?? drawerConn.lastUsedAt)}</span></div>
                  <div>Dernière rotation : <span className="font-mono">{formatShortDate(usageData?.lastRotatedAt ?? drawerConn.lastRotatedAt)}</span></div>
                  <div>Expiration secret : <span className="font-mono">{formatShortDate(usageData?.secretExpiresAt ?? drawerConn.secretExpiresAt)}</span></div>
                </div>
                {(usageData?.lastErrorSanitized || drawerConn.lastErrorSanitized) && (
                  <div className="mt-2 p-2 rounded-lg bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300 font-mono text-[11px]">
                    {usageData?.lastErrorSanitized || drawerConn.lastErrorSanitized}
                  </div>
                )}
              </div>

              {/* Secret Rotation Form */}
              <form onSubmit={handleRotateSecret} className="p-4 rounded-2xl bg-amber-50/50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800/70 space-y-3">
                <div className="flex items-center gap-2 font-extrabold text-[#0F172A] dark:text-white">
                  <RefreshCw className="w-4 h-4 text-amber-600 dark:text-amber-400" />
                  <span>Rotation du secret</span>
                </div>
                <div>
                  <label htmlFor="rotate-creds-input" className="font-bold block mb-1">
                    Nouveau Credentials JSON (chiffré)
                  </label>
                  <input
                    id="rotate-creds-input"
                    type="password"
                    value={rotateCredentialsJson}
                    onChange={(e) => setRotateCredentialsJson(e.target.value)}
                    placeholder='{"token":"new-secret-value"}'
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                  />
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label htmlFor="rotate-extkey-input" className="font-bold block mb-1">
                      External Vault Key (optionnel)
                    </label>
                    <input
                      id="rotate-extkey-input"
                      type="text"
                      value={rotateExternalKey}
                      onChange={(e) => setRotateExternalKey(e.target.value)}
                      placeholder="KEY_V2"
                      className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                    />
                  </div>
                  <div>
                    <label htmlFor="rotate-expiry-input" className="font-bold block mb-1">
                      Date d&apos;expiration
                    </label>
                    <input
                      id="rotate-expiry-input"
                      type="date"
                      value={rotateExpiresAt}
                      onChange={(e) => setRotateExpiresAt(e.target.value)}
                      className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono"
                    />
                  </div>
                </div>
                <button
                  type="submit"
                  disabled={isRotating}
                  className="w-full px-3 py-2 bg-amber-600 hover:bg-amber-700 disabled:opacity-50 text-white rounded-xl font-bold inline-flex items-center justify-center gap-1.5 cursor-pointer"
                >
                  <RefreshCw className="w-3.5 h-3.5" />
                  <span>{isRotating ? 'Rotation en cours...' : 'Rotation du secret'}</span>
                </button>
              </form>

              {/* Usage History Table */}
              <div className="space-y-2">
                <h4 className="font-extrabold text-[#0F172A] dark:text-white flex items-center gap-1.5">
                  <Clock className="w-4 h-4 text-slate-500" />
                  <span>Historique d&apos;utilisation ({usageData?.logs.length ?? 0})</span>
                </h4>
                {usageLoading ? (
                  <p className="text-slate-400">Chargement de l&apos;historique...</p>
                ) : !usageData || usageData.logs.length === 0 ? (
                  <p className="text-slate-400">Aucune invocation enregistrée pour cette connexion.</p>
                ) : (
                  <ul className="space-y-2">
                    {usageData.logs.map((log) => (
                      <li
                        key={log.id}
                        className="p-3 rounded-xl bg-[#F8FAFC] dark:bg-slate-900/70 border border-[#E2E8F0] dark:border-slate-700 flex items-center justify-between gap-2"
                      >
                        <div className="space-y-0.5 min-w-0">
                          <div className="flex items-center gap-1.5">
                            <span
                              className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${
                                log.success
                                  ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300'
                                  : 'bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300'
                              }`}
                            >
                              {log.success ? 'OK' : 'ÉCHEC'}
                            </span>
                            <span className="font-mono font-semibold text-[#0F172A] dark:text-slate-100 truncate">
                              {log.actorId}
                            </span>
                            <span className="text-[10px] text-slate-400 font-mono">
                              {log.durationMs} ms
                            </span>
                          </div>
                          {(log.workflowKey || log.instanceId) && (
                            <div className="text-[10px] font-mono text-slate-500 truncate">
                              {log.workflowKey || '-'} {log.nodeId ? `· ${log.nodeId}` : ''} {log.instanceId ? `(${log.instanceId})` : ''}
                            </div>
                          )}
                        </div>
                        <span className="text-[10px] text-slate-400 shrink-0 font-mono">
                          {formatShortDate(log.executedAt)}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </aside>
        </div>
      )}

      {/* Personal Credential Vault (Mes Identifiants Externes) */}
      <PersonalVaultPanel />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Card 1: SMTP Mail Connector */}
        <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-4 transition-colors">
          <div className="flex items-center gap-2 pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
            <Mail className="w-5 h-5 text-amber-500" />
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">SMTP Email Gateway Config</h3>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <label htmlFor="settings-smtp-host" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">SMTP Host Hostname</label>
              <input
                id="settings-smtp-host"
                type="text"
                value={settings.smtpHost}
                onChange={(e) => setSettings({ ...settings, smtpHost: e.target.value })}
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label htmlFor="settings-smtp-port" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Port</label>
                <input
                  id="settings-smtp-port"
                  type="text"
                  value={settings.smtpPort}
                  onChange={(e) => setSettings({ ...settings, smtpPort: e.target.value })}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
                />
              </div>

              <div>
                <label htmlFor="settings-smtp-sec" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">SSL / TLS</label>
                <select
                  id="settings-smtp-sec"
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
              <label htmlFor="settings-sql-conn" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">SQL Connection String</label>
              <input
                id="settings-sql-conn"
                type="password"
                value={settings.sqlConnectionString}
                onChange={(e) => setSettings({ ...settings, sqlConnectionString: e.target.value })}
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label htmlFor="settings-sql-pool" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Max Pool Size</label>
                <input
                  id="settings-sql-pool"
                  type="number"
                  value={settings.sqlMaxPoolSize}
                  onChange={(e) => setSettings({ ...settings, sqlMaxPoolSize: parseInt(e.target.value, 10) || 0 })}
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs text-[#0F172A] dark:text-slate-100 font-mono"
                />
              </div>

              <div>
                <label htmlFor="settings-sql-timeout" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Timeout (Sec)</label>
                <input
                  id="settings-sql-timeout"
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
