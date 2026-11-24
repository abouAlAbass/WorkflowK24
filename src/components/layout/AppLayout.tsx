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
  Languages
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useTheme } from '../../hooks/useTheme';
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
    savedWorkflows 
  } = useWorkflowStore();

  const { theme, toggleTheme, designTheme, language, setLanguage, direction, toggleDirection, t } = useTheme();
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [isEnvDropdownOpen, setIsEnvDropdownOpen] = useState(false);
  const [isLangDropdownOpen, setIsLangDropdownOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  const environments: Environment[] = ['Development', 'Staging', 'Production'];

  const navItems: Array<{
    id: NavigationTab;
    label: string;
    icon: React.ReactNode;
    badge?: number;
  }> = [
    { id: 'dashboard', label: 'Home Dashboard', icon: <Home className="w-4 h-4" /> },
    { 
      id: 'designer', 
      label: 'Workflow Designer', 
      icon: <Workflow className="w-4 h-4" />,
      badge: savedWorkflows.length > 0 ? savedWorkflows.length : undefined
    },
    { 
      id: 'instances', 
      label: 'Active Instances', 
      icon: <Activity className="w-4 h-4" />,
      badge: activeInstances.filter(i => i.status === 'RUNNING' || i.status === 'PENDING').length
    },
    { id: 'smartobjects', label: 'Data Models (SmartObjects)', icon: <Database className="w-4 h-4" /> },
    { 
      id: 'tasks', 
      label: 'Task Center', 
      icon: <Inbox className="w-4 h-4" />,
      badge: actionItemTasks.length > 0 ? actionItemTasks.length : undefined 
    },
    { id: 'analytics', label: 'Reports & Analytics', icon: <BarChart3 className="w-4 h-4" /> },
    { id: 'users', label: 'Users & Access', icon: <Users className="w-4 h-4" /> },
    { id: 'settings', label: 'System Settings', icon: <Settings className="w-4 h-4" /> },
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
              <h1 className="font-semibold text-sm text-[#0F172A] dark:text-white tracking-tight">
                {t.common.appName} <span className="text-[#2563EB] dark:text-blue-400 font-bold">{t.common.studioName}</span>
              </h1>
              <span className="text-[10px] font-mono font-medium px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-[#475569] dark:text-slate-300 border border-slate-200 dark:border-slate-700">
                {t.common.version}
              </span>
            </div>
            <p className="text-[11px] text-[#475569] dark:text-slate-400 font-medium">
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
          {/* Design System Preset Switcher (Corporate, Microsoft, Apple, SAP) */}
          <ThemeSelector />

          {/* Language & Direction Switcher (Fix #10) */}
          <div className="relative">
            <button
              onClick={() => setIsLangDropdownOpen(!isLangDropdownOpen)}
              aria-label="Switch Language and Direction"
              className="p-2 rounded-xl border border-[#CBD5E1] dark:border-[#1F2937] bg-white dark:bg-[#111827] hover:bg-slate-100 dark:hover:bg-slate-800 text-[#0F172A] dark:text-slate-100 shadow-2xs transition-colors flex items-center gap-1.5 text-xs font-semibold cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
              title="Change Language & Direction"
            >
              <Languages className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
              <span className="uppercase font-mono text-[11px]">{language}</span>
              <ChevronDown className="w-3 h-3 opacity-70" />
            </button>

            {isLangDropdownOpen && (
              <div className="absolute end-0 top-full mt-1.5 w-44 bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] rounded-2xl shadow-lg p-1.5 z-50 animate-fade-in">
                <span className="text-[10px] font-medium text-[#475569] dark:text-slate-400 uppercase tracking-wider px-2 py-1 block">
                  Select Language
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
                  Select Environment
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

          {/* 3. Persistent Single Canonical Dark / Light Mode Toggle Button with tooltip & aria-label */}
          <button
            onClick={toggleTheme}
            aria-label={theme === 'dark' ? t.common.switchToLight : t.common.switchToDark}
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
              title="System Alerts & Notifications"
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
                  <h4 className="text-xs font-semibold text-[#0F172A] dark:text-white">Notifications</h4>
                  <span className="text-[10px] text-[#2563EB] font-medium">{actionItemTasks.length} Pending</span>
                </div>
                <div className="space-y-2 mt-2 max-h-56 overflow-y-auto">
                  {actionItemTasks.map((t) => (
                    <div
                      key={t.id}
                      onClick={() => {
                        setCurrentTab('tasks');
                        setIsNotificationsOpen(false);
                      }}
                      className="p-2 rounded-xl border border-[#E2E8F0] dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/80 hover:bg-blue-50/50 dark:hover:bg-blue-950/40 cursor-pointer transition-colors"
                    >
                      <div className="flex items-center gap-1.5 text-xs font-semibold text-[#0F172A] dark:text-slate-100">
                        <AlertCircle className="w-3.5 h-3.5 text-amber-500 shrink-0" />
                        <span className="truncate">{t.title}</span>
                      </div>
                      <span className="text-[10px] text-[#475569] dark:text-slate-400 block mt-0.5 font-medium">
                        Requested by {t.requester} • Due {t.dueDate}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* User Profile Avatar */}
          <div className="flex items-center gap-2.5 ps-2.5 border-s border-[#E2E8F0] dark:border-[#1F2937]">
            <img
              src="/avatars/user-1.svg"
              alt="Mohamed Fetni"
              className="w-8 h-8 rounded-full border border-slate-300 dark:border-slate-700 object-cover shadow-2xs"
            />
            <div className="hidden lg:block text-start">
              <span className="text-xs font-semibold text-[#0F172A] dark:text-slate-100 block leading-none">
                Mohamed Fetni
              </span>
              <span className="text-[10px] text-[#475569] dark:text-slate-400 font-medium block mt-0.5">
                BPM Administrator
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* Main Workspace Body */}
      <div className="flex-1 flex overflow-hidden relative">
        {/* Sidebar Navigation */}
        <aside
          className={`${
            isSidebarCollapsed ? 'w-16' : 'w-64'
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
                  title={isSidebarCollapsed ? item.label : undefined}
                  className={`w-full flex items-center justify-between p-2.5 rounded-xl text-xs font-medium transition-all cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                    isActive
                      ? 'bg-[#2563EB] text-white font-semibold shadow-2xs'
                      : 'text-[#475569] dark:text-slate-300 hover:bg-[#F1F5F9] dark:hover:bg-slate-800/80 hover:text-[#0F172A] dark:hover:text-slate-100'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <span className={isActive ? 'text-white' : 'text-[#475569] dark:text-slate-400'}>
                      {item.icon}
                    </span>
                    {!isSidebarCollapsed && (
                      <span className="truncate">{item.label}</span>
                    )}
                  </div>

                  {!isSidebarCollapsed && item.badge !== undefined && (
                    <span
                      className={`text-[10px] font-mono font-medium px-2 py-0.5 rounded-full ${
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
                Tamam Workflow Engine
              </span>
            )}
            <button
              onClick={() => setIsSidebarCollapsed(!isSidebarCollapsed)}
              className="p-1.5 rounded-lg text-[#64748B] hover:text-[#0F172A] dark:hover:text-slate-100 hover:bg-[#F1F5F9] dark:hover:bg-slate-800 transition-colors mx-auto cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
              title={isSidebarCollapsed ? 'Expand Sidebar' : 'Collapse Sidebar'}
              aria-label={isSidebarCollapsed ? 'Expand Sidebar' : 'Collapse Sidebar'}
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
