import React, { useState } from 'react';
import {
  Workflow,
  Home,
  Activity,
  Database,
  Inbox,
  BarChart3,
  Users,
  Settings,
  Sun,
  Moon,
  Search,
  Bell,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ShieldCheck,
  CheckCircle2,
  AlertCircle,
  Languages,
  LogOut,
  KeyRound,
  Server,
  FileSpreadsheet,
  Store
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';
import { useTheme } from '../../hooks/useTheme';
import { getWorkflowRepository, repositoryManager } from '../../services/repositoryManager';
import type { NavigationTab, Environment } from '../../types/workflow';
import { ThemeSelector } from '../header/ThemeSelector';

interface AppLayoutProps {
  children: React.ReactNode;
}

export const AppLayout: React.FC<AppLayoutProps> = ({ children }) => {
  const {
    currentTab,
    setCurrentTab,
    currentEnv,
    setCurrentEnv,
    actionItemTasks,
    activeInstances,
    savedWorkflows,
    appMode,
    openModeModal,
    syncDataFromRepository
  } = useWorkflowStore();

  const { theme, toggleTheme, designTheme, language, setLanguage, direction, toggleDirection, t } = useTheme();
  const smartFormsCount = useSmartFormStore((s) => s.forms.length);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [isEnvDropdownOpen, setIsEnvDropdownOpen] = useState(false);
  const [isLangDropdownOpen, setIsLangDropdownOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [isProfileMenuOpen, setIsProfileMenuOpen] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [passwordFeedback, setPasswordFeedback] = useState<{ type: 'ok' | 'err'; text: string } | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  const repo = getWorkflowRepository();
  const authSession = repo.getAuthSession();
  const isConnected = (appMode || repositoryManager.getMode()) === 'connected' && authSession !== null;

  const activeUser = isConnected && authSession
    ? authSession.user
    : {
        id: 'demo-admin',
        name: 'Mohamed Fetni',
        email: 'mohamed.fetni@enterprise.com',
        role: 'BPM Administrator' as const,
        group: 'Global Admin Group',
        status: 'Active' as const,
        avatarUrl: '/avatars/user-1.svg'
      };

  const environments: Environment[] = ['Development', 'Staging', 'Production'];

  const navItems: Array<{
    id: NavigationTab;
    label: string;
    icon: React.ReactNode;
    badge?: number;
  }> = [
    { id: 'dashboard', label: t.nav.dashboard, icon: <Home className="w-4 h-4" /> },
    { id: 'portal', label: t.nav.portal, icon: <Store className="w-4 h-4" /> },
    {
      id: 'designer',
      label: t.nav.designer,
      icon: <Workflow className="w-4 h-4" />,
      badge: savedWorkflows.length > 0 ? savedWorkflows.length : undefined
    },
    {
      id: 'smartforms',
      label: t.nav.smartforms,
      icon: <FileSpreadsheet className="w-4 h-4" />,
      badge: smartFormsCount > 0 ? smartFormsCount : undefined
    },
    {
      id: 'instances',
      label: t.nav.instances,
      icon: <Activity className="w-4 h-4" />,
      badge: activeInstances.filter(i => i.status === 'RUNNING' || i.status === 'PENDING').length
    },
    { id: 'smartobjects', label: t.nav.smartobjects, icon: <Database className="w-4 h-4" /> },
    {
      id: 'tasks',
      label: t.nav.tasks,
      icon: <Inbox className="w-4 h-4" />,
      badge: actionItemTasks.length > 0 ? actionItemTasks.length : undefined
    },
    { id: 'analytics', label: t.nav.analytics, icon: <BarChart3 className="w-4 h-4" /> },
    { id: 'users', label: t.nav.users, icon: <Users className="w-4 h-4" /> },
    { id: 'settings', label: t.nav.settings, icon: <Settings className="w-4 h-4" /> },
  ];

  const getEnvBadgeStyle = (env: Environment) => {
    switch (env) {
      case 'Production':
        return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border-emerald-300 dark:border-emerald-800';
      case 'Staging':
        return 'bg-amber-500/10 text-amber-700 dark:text-amber-400 border-amber-300 dark:border-amber-800';
      default:
        return 'bg-blue-500/10 text-blue-700 dark:text-blue-400 border-blue-300 dark:border-blue-800';
    }
  };

  const handleSignOut = async () => {
    repo.logout();
    repositoryManager.setMode('demo');
    setIsProfileMenuOpen(false);
    setIsChangingPassword(false);
    if (typeof syncDataFromRepository === 'function') {
      await syncDataFromRepository();
    }
    if (typeof openModeModal === 'function') {
      openModeModal();
    }
  };

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordFeedback(null);
    try {
      if (typeof repo.changePassword === 'function') {
        await repo.changePassword(currentPassword, newPassword);
      }
      setPasswordFeedback({ type: 'ok', text: t.profile.passwordUpdated });
      setCurrentPassword('');
      setNewPassword('');
      setTimeout(() => {
        setIsChangingPassword(false);
        setPasswordFeedback(null);
      }, 1500);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error updating password';
      setPasswordFeedback({ type: 'err', text: msg });
    }
  };

  return (
    <div
      dir={direction}
      data-design-theme={designTheme}
      className={`w-screen h-screen flex flex-col overflow-hidden antialiased bg-[#F1F5F9] dark:bg-[#0B0F19] text-[#0F172A] dark:text-[#F8FAFC] transition-colors duration-200 theme-${designTheme}`}
      style={{ fontFamily: 'var(--theme-font-family)' }}
    >
      {/* Executive Top Header Navigation */}
      <header className="h-16 bg-white dark:bg-[#111827] border-b border-[#E2E8F0] dark:border-[#1F2937] px-4 flex items-center justify-between z-30 select-none shadow-2xs transition-colors duration-200">
        {/* Brand & App Title */}
        <div className="flex items-center gap-3">
          <div className="p-2 rounded-xl bg-slate-900 dark:bg-blue-600 text-white shadow-xs flex items-center justify-center shrink-0">
            <Workflow className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-sm ty-logo-name text-[#0F172A] dark:text-white">
                {t.common.appName} <span className="ty-logo-studio">{t.common.studioName}</span>
              </h1>
              <span className="ty-badge num-tabular rounded-full bg-slate-100 dark:bg-slate-800 text-[#475569] dark:text-slate-300 border border-slate-200 dark:border-slate-700">
                {t.common.version}
              </span>
            </div>
            <p className="ty-logo-sub">
              {t.common.tagline}
            </p>
          </div>
        </div>

        {/* Center Global Search (WCAG AA Compliant contrast) */}
        <div className="hidden md:flex items-center flex-1 max-w-md mx-8 relative">
          <Search className="w-4 h-4 text-[#64748B] dark:text-slate-400 absolute start-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={t.common.searchPlaceholder}
            aria-label="Search workflows and tasks"
            className="w-full bg-[#F8FAFC] dark:bg-slate-800/60 border border-[#CBD5E1] dark:border-slate-700 rounded-xl ps-10 pe-12 py-1.5 text-xs text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-[#2563EB] transition-all"
          />
          <kbd className="absolute end-3 top-1/2 -translate-y-1/2 text-[10px] font-mono font-medium text-[#64748B] dark:text-slate-400 bg-white dark:bg-slate-900 px-1.5 py-0.5 rounded border border-[#CBD5E1] dark:border-slate-700 shadow-2xs">
            ⌘K
          </kbd>
        </div>

        {/* Right Top Bar Actions */}
        <div className="flex items-center gap-2">
          {/* Design System Preset Switcher */}
          <ThemeSelector />

          {/* Language & Direction Switcher */}
          <div className="relative">
            <button
              onClick={() => setIsLangDropdownOpen(!isLangDropdownOpen)}
              aria-label="Switch Language and Direction"
              className="p-2 rounded-xl border border-[#CBD5E1] dark:border-[#1F2937] bg-white dark:bg-[#111827] hover:bg-slate-100 dark:hover:bg-slate-800 text-[#0F172A] dark:text-slate-100 shadow-2xs transition-colors flex items-center gap-1.5 text-xs font-semibold cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
              title={t.common.language}
            >
              <Languages className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
              <span className="uppercase font-mono text-[11px]">{language}</span>
              <ChevronDown className="w-3 h-3 opacity-70" />
            </button>

            {isLangDropdownOpen && (
              <div className="absolute end-0 top-full mt-1.5 w-44 bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] rounded-2xl shadow-lg p-1.5 z-50 animate-fade-in">
                <span className="text-[10px] font-medium text-[#475569] dark:text-slate-400 uppercase tracking-wider px-2 py-1 block">
                  {t.profile.selectLanguage}
                </span>
                <button
                  onClick={() => {
                    setLanguage('en');
                    setIsLangDropdownOpen(false);
                  }}
                  className={`w-full text-start px-2.5 py-1.5 text-xs font-medium rounded-xl flex items-center justify-between transition-colors cursor-pointer ${
                    language === 'en'
                      ? 'bg-blue-500/10 text-[#2563EB] dark:text-blue-400 font-semibold'
                      : 'text-[#0F172A] dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800'
                  }`}
                >
                  <span>English (LTR)</span>
                  {language === 'en' && <CheckCircle2 className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />}
                </button>
                <button
                  onClick={() => {
                    setLanguage('fr');
                    setIsLangDropdownOpen(false);
                  }}
                  className={`w-full text-start px-2.5 py-1.5 text-xs font-medium rounded-xl flex items-center justify-between transition-colors cursor-pointer ${
                    language === 'fr'
                      ? 'bg-blue-500/10 text-[#2563EB] dark:text-blue-400 font-semibold'
                      : 'text-[#0F172A] dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800'
                  }`}
                >
                  <span>Français (LTR)</span>
                  {language === 'fr' && <CheckCircle2 className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />}
                </button>
                <button
                  onClick={() => {
                    setLanguage('ar');
                    setIsLangDropdownOpen(false);
                  }}
                  className={`w-full text-start px-2.5 py-1.5 text-xs font-medium rounded-xl flex items-center justify-between transition-colors cursor-pointer ${
                    language === 'ar'
                      ? 'bg-blue-500/10 text-[#2563EB] dark:text-blue-400 font-semibold'
                      : 'text-[#0F172A] dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800'
                  }`}
                >
                  <span>العربية (RTL)</span>
                  {language === 'ar' && <CheckCircle2 className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />}
                </button>

                <div className="my-1 border-t border-slate-100 dark:border-slate-800" />
                <button
                  onClick={() => {
                    toggleDirection();
                    setIsLangDropdownOpen(false);
                  }}
                  className="w-full text-start px-2.5 py-1.5 text-[11px] font-semibold text-[#475569] dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl transition-colors cursor-pointer"
                >
                  ⇄ {t.common.directionToggle} ({direction.toUpperCase()})
                </button>
              </div>
            )}
          </div>

          {/* Environment Switcher */}
          <div className="relative">
            <button
              onClick={() => setIsEnvDropdownOpen(!isEnvDropdownOpen)}
              aria-label={`Current Environment: ${currentEnv}. Click to change.`}
              className={`px-3 py-1.5 rounded-xl border text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500 ${getEnvBadgeStyle(currentEnv)}`}
            >
              <ShieldCheck className="w-3.5 h-3.5" />
              <span>{currentEnv}</span>
              <ChevronDown className="w-3.5 h-3.5 opacity-70" />
            </button>

            {isEnvDropdownOpen && (
              <div className="absolute end-0 top-full mt-1.5 w-44 bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] rounded-2xl shadow-lg p-1.5 z-50 animate-fade-in">
                <span className="text-[10px] font-medium text-[#475569] dark:text-slate-400 uppercase tracking-wider px-2 py-1 block">
                  {t.profile.selectEnvironment}
                </span>
                {environments.map((env) => (
                  <button
                    key={env}
                    onClick={() => {
                      setCurrentEnv(env);
                      setIsEnvDropdownOpen(false);
                    }}
                    className={`w-full text-start px-2.5 py-1.5 text-xs font-medium rounded-xl flex items-center justify-between transition-colors cursor-pointer ${
                      currentEnv === env
                        ? 'bg-blue-500/10 text-[#2563EB] dark:text-blue-400 font-semibold'
                        : 'text-[#0F172A] dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800'
                    }`}
                  >
                    <span>{env}</span>
                    {currentEnv === env && <CheckCircle2 className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Canonical Dark / Light Mode Toggle */}
          <button
            onClick={toggleTheme}
            aria-label={theme === 'dark' ? 'Switch to Light Mode' : 'Switch to Dark Mode'}
            className="p-2 rounded-xl border border-[#CBD5E1] dark:border-[#1F2937] bg-white dark:bg-[#111827] hover:bg-slate-100 dark:hover:bg-slate-800 text-[#0F172A] dark:text-slate-100 shadow-2xs transition-colors flex items-center justify-center cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
            title={theme === 'dark' ? t.common.switchToLight : t.common.switchToDark}
          >
            {theme === 'dark' ? (
              <Sun className="w-4 h-4 text-amber-400" />
            ) : (
              <Moon className="w-4 h-4 text-slate-700" />
            )}
          </button>

          {/* Notifications Trigger */}
          <div className="relative">
            <button
              onClick={() => setIsNotificationsOpen(!isNotificationsOpen)}
              aria-label={`System Notifications. ${actionItemTasks.length} pending.`}
              className="p-2 rounded-xl border border-[#CBD5E1] dark:border-[#1F2937] bg-white dark:bg-[#111827] hover:bg-slate-100 dark:hover:bg-slate-800 text-[#0F172A] dark:text-slate-100 shadow-2xs transition-colors relative cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
              title={t.common.notifications}
            >
              <Bell className="w-4 h-4" />
              {actionItemTasks.length > 0 && (
                <span className="absolute -top-1 -end-1 w-4 h-4 rounded-full bg-rose-500 text-white text-[9px] font-bold flex items-center justify-center">
                  {actionItemTasks.length}
                </span>
              )}
            </button>

            {isNotificationsOpen && (
              <div className="absolute end-0 top-full mt-1.5 w-80 bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] rounded-2xl shadow-xl p-3 z-50 animate-fade-in">
                <div className="flex items-center justify-between pb-2 border-b border-[#E2E8F0] dark:border-[#1F2937]">
                  <h4 className="text-xs font-semibold text-[#0F172A] dark:text-white">{t.common.notifications}</h4>
                  <span className="text-[10px] text-[#2563EB] font-medium">{actionItemTasks.length} {t.common.pending}</span>
                </div>
                <div className="space-y-2 mt-2 max-h-56 overflow-y-auto">
                  {actionItemTasks.length === 0 ? (
                    <p className="text-xs text-[#64748B] dark:text-slate-400 py-3 text-center">
                      {t.profile.noPendingNotifications}
                    </p>
                  ) : (
                    actionItemTasks.map((task) => (
                      <div
                        key={task.id}
                        onClick={() => {
                          setCurrentTab('tasks');
                          setIsNotificationsOpen(false);
                        }}
                        className="p-2 rounded-xl border border-[#E2E8F0] dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/80 hover:bg-blue-50/50 dark:hover:bg-blue-950/40 cursor-pointer transition-colors"
                      >
                        <div className="flex items-center gap-1.5 text-xs font-semibold text-[#0F172A] dark:text-slate-100">
                          <AlertCircle className="w-3.5 h-3.5 text-amber-500 shrink-0" />
                          <span className="truncate">{task.title}</span>
                        </div>
                        <span className="text-[10px] text-[#475569] dark:text-slate-400 block mt-0.5 font-medium">
                          {t.profile.requestedBy} {task.requester} • {t.profile.due} {task.dueDate}
                        </span>
                      </div>
                    ))
                  )}
                </div>
              </div>
            )}
          </div>

          {/* Authenticated User Profile Menu */}
          <div className="relative ps-2.5 border-s border-[#E2E8F0] dark:border-[#1F2937]">
            <button
              type="button"
              onClick={() => {
                setIsProfileMenuOpen(!isProfileMenuOpen);
                setIsChangingPassword(false);
                setPasswordFeedback(null);
              }}
              aria-label={`User Profile: ${activeUser.name}`}
              className="flex items-center gap-2.5 rounded-xl px-1.5 py-1 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <img
                src={activeUser.avatarUrl || '/avatars/user-1.svg'}
                alt={activeUser.name}
                className="w-8 h-8 rounded-full border border-slate-300 dark:border-slate-700 object-cover shadow-2xs"
              />
              <div className="hidden lg:block text-start">
                <span className="text-xs font-semibold text-[#0F172A] dark:text-slate-100 block leading-none">
                  {activeUser.name}
                </span>
                <span className="text-[10px] text-[#475569] dark:text-slate-400 font-medium block mt-0.5">
                  {activeUser.role}
                </span>
              </div>
              <ChevronDown className="w-3.5 h-3.5 text-slate-400 hidden lg:block" />
            </button>

            {isProfileMenuOpen && (
              <div className="absolute end-0 top-full mt-1.5 w-72 bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] rounded-2xl shadow-xl p-3 z-50 animate-fade-in space-y-2.5">
                <div className="pb-2.5 border-b border-[#E2E8F0] dark:border-[#1F2937]">
                  <p className="text-xs font-bold text-[#0F172A] dark:text-white truncate">{activeUser.name}</p>
                  <p className="text-[11px] text-[#64748B] dark:text-slate-400 truncate font-mono">{activeUser.email}</p>
                  <span className={`inline-flex items-center gap-1 mt-1.5 px-2 py-0.5 rounded-full text-[10px] font-semibold ${
                    isConnected
                      ? 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20'
                      : 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20'
                  }`}>
                    <span className={`w-1.5 h-1.5 rounded-full ${isConnected ? 'bg-indigo-500' : 'bg-emerald-500'}`} />
                    {isConnected ? t.profile.connectedSession : t.profile.demoSession}
                  </span>
                </div>

                {!isChangingPassword ? (
                  <div className="space-y-1">
                    <button
                      type="button"
                      onClick={() => {
                        setIsProfileMenuOpen(false);
                        if (typeof openModeModal === 'function') openModeModal();
                      }}
                      className="w-full text-start px-2.5 py-2 rounded-xl text-xs font-medium text-[#0F172A] dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center gap-2 cursor-pointer transition-colors"
                    >
                      <Server className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />
                      <span>{t.profile.switchRuntimeMode}</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => setIsChangingPassword(true)}
                      className="w-full text-start px-2.5 py-2 rounded-xl text-xs font-medium text-[#0F172A] dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center gap-2 cursor-pointer transition-colors"
                    >
                      <KeyRound className="w-3.5 h-3.5 text-amber-500" />
                      <span>{t.profile.changePassword}</span>
                    </button>

                    {isConnected && (
                      <button
                        type="button"
                        onClick={handleSignOut}
                        className="w-full text-start px-2.5 py-2 rounded-xl text-xs font-semibold text-rose-600 dark:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/40 flex items-center gap-2 cursor-pointer transition-colors"
                      >
                        <LogOut className="w-3.5 h-3.5" />
                        <span>{t.profile.signOut}</span>
                      </button>
                    )}
                  </div>
                ) : (
                  <form onSubmit={handlePasswordSubmit} className="space-y-2">
                    {passwordFeedback && (
                      <div className={`p-2 rounded-lg text-[11px] font-medium ${
                        passwordFeedback.type === 'ok'
                          ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                          : 'bg-rose-500/10 text-rose-600 dark:text-rose-400'
                      }`}>
                        {passwordFeedback.text}
                      </div>
                    )}
                    <input
                      type="password"
                      required
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder={t.profile.currentPassword}
                      className="w-full px-2.5 py-1.5 rounded-lg text-xs bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700"
                    />
                    <input
                      type="password"
                      required
                      minLength={8}
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder={t.profile.newPassword}
                      className="w-full px-2.5 py-1.5 rounded-lg text-xs bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700"
                    />
                    <div className="flex justify-end gap-1.5 pt-1">
                      <button
                        type="button"
                        onClick={() => setIsChangingPassword(false)}
                        className="px-2.5 py-1 text-[11px] font-medium text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 cursor-pointer"
                      >
                        {t.profile.cancel}
                      </button>
                      <button
                        type="submit"
                        className="px-3 py-1 rounded-lg bg-[#2563EB] hover:bg-blue-700 text-white text-[11px] font-semibold cursor-pointer"
                      >
                        {t.profile.savePassword}
                      </button>
                    </div>
                  </form>
                )}
              </div>
            )}
          </div>
        </div>
      </header>

      {/* Main Workspace Body */}
      <div className="flex-1 flex overflow-hidden relative">
        {/* Sidebar Navigation */}
        <aside
          className={`${
            isSidebarCollapsed ? 'w-16' : 'app-sidebar-expanded shrink-0'
          } bg-white dark:bg-[#111827] border-e border-[#E2E8F0] dark:border-[#1F2937] flex flex-col h-full z-20 select-none shadow-2xs transition-all duration-200 text-[#0F172A] dark:text-slate-100`}
        >
          {/* Nav Items List */}
          <div className="flex-1 overflow-y-auto p-2.5 space-y-1">
            {navItems.map((item) => {
              const isActive = currentTab === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => setCurrentTab(item.id)}
                  title={item.label}
                  aria-current={isActive ? 'page' : undefined}
                  className={`ty-nav w-full flex items-center justify-between gap-2 px-2.5 py-2 rounded-xl transition-all cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                    isActive
                      ? 'bg-[#2563EB] text-white shadow-2xs'
                      : 'text-[#475569] dark:text-slate-300 hover:bg-[#F1F5F9] dark:hover:bg-slate-800/80 hover:text-[#0F172A] dark:hover:text-slate-100'
                  }`}
                >
                  <div className="flex items-center gap-3 min-w-0 flex-1 text-start">
                    <span className={`shrink-0 ${isActive ? 'text-white' : 'text-[#475569] dark:text-slate-400'}`}>
                      {item.icon}
                    </span>
                    {!isSidebarCollapsed && (
                      <span className="min-w-0 line-clamp-2 break-words">{item.label}</span>
                    )}
                  </div>

                  {!isSidebarCollapsed && item.badge !== undefined && (
                    <span
                      className={`ty-badge num-tabular rounded-full ${
                        isActive
                          ? 'bg-white/20 text-white'
                          : 'bg-slate-100 dark:bg-slate-800 text-[#475569] dark:text-slate-300 border border-slate-200 dark:border-slate-700'
                      }`}
                    >
                      {item.badge}
                    </span>
                  )}
                </button>
              );
            })}
          </div>

          {/* Bottom Sidebar Footer Toggle */}
          <div className="p-2.5 border-t border-[#E2E8F0] dark:border-[#1F2937] flex items-center justify-between">
            {!isSidebarCollapsed && (
              <span className="text-[10px] font-mono text-[#64748B] dark:text-slate-400 font-medium px-2">
                {t.nav.engineFooter}
              </span>
            )}
            <button
              onClick={() => setIsSidebarCollapsed(!isSidebarCollapsed)}
              className="p-1.5 rounded-lg text-[#64748B] hover:text-[#0F172A] dark:hover:text-slate-100 hover:bg-[#F1F5F9] dark:hover:bg-slate-800 transition-colors mx-auto cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
              title={isSidebarCollapsed ? t.nav.expandSidebar : t.nav.collapseSidebar}
              aria-label={isSidebarCollapsed ? t.nav.expandSidebar : t.nav.collapseSidebar}
            >
              {isSidebarCollapsed ? (
                <ChevronRight className="w-4 h-4 rtl:rotate-180" />
              ) : (
                <ChevronLeft className="w-4 h-4 rtl:rotate-180" />
              )}
            </button>
          </div>
        </aside>

        {/* View Content Region */}
        <main className="flex-1 flex flex-col overflow-hidden bg-[#F1F5F9] dark:bg-[#0B0F19] transition-colors duration-200 relative">
          {children}
        </main>
      </div>
    </div>
  );
};
