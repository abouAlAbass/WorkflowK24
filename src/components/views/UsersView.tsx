import React, { useState } from 'react';
import { 
  Users, 
  UserPlus, 
  Shield, 
  CheckCircle2, 
  XCircle, 
  Trash2, 
  Search, 
  X, 
  Mail, 
  UserCheck, 
  Layers,
  Pencil,
  KeyRound,
  Power,
  History
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { AuthProvidersPanel } from './AuthProvidersPanel';
import type { UserRecord, UserAuditEntry } from '../../types/workflow';
import { getWorkflowRepository, repositoryManager } from '../../services/repositoryManager';
import {
  ALL_ROLES,
  ASSIGNABLE_ROLES,
  PASSWORD_RULE_TEXT,
  PERMISSIONS,
  hasPermission,
  isValidPassword,
  type Role
} from '../../types/roles';

const DEFAULT_AVATARS = [
  '/avatars/user-2.svg',
  '/avatars/user-3.svg',
  '/avatars/user-5.svg',
  '/avatars/user-1.svg',
  '/avatars/user-4.svg'
];

export const UsersView: React.FC = () => {
  const { users, addUser, updateUser, resetUserPassword, deleteUser } = useWorkflowStore();

  // Connected mode: permissions come from the signed-in role (users.manage = principal administrator).
  // Demo mode is a local simulation, so management stays available.
  const session = getWorkflowRepository().getAuthSession();
  const isConnectedMode = repositoryManager.getMode() === 'connected';
  const canManageUsers =
    !isConnectedMode || hasPermission(session?.user?.role, PERMISSIONS.UsersManage);

  const [password, setPassword] = useState('');

  // Edit / reset-password / audit state
  const [editingUser, setEditingUser] = useState<UserRecord | null>(null);
  const [editName, setEditName] = useState('');
  const [editRole, setEditRole] = useState<Role>('End User');
  const [editGroup, setEditGroup] = useState('');
  const [editStatus, setEditStatus] = useState<'Active' | 'Inactive'>('Active');
  const [resetTarget, setResetTarget] = useState<UserRecord | null>(null);
  const [resetPassword, setResetPassword] = useState('');
  const [modalError, setModalError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [auditEntries, setAuditEntries] = useState<UserAuditEntry[]>([]);
  const [showAudit, setShowAudit] = useState(false);

  const loadAudit = async () => {
    try {
      setAuditEntries(await getWorkflowRepository().getUserAuditLog(50));
    } catch {
      setAuditEntries([]);
    }
  };

  const openEdit = (u: UserRecord) => {
    setEditingUser(u);
    setEditName(u.name);
    setEditRole(u.role);
    setEditGroup(u.group);
    setEditStatus(u.status);
    setModalError(null);
  };

  const submitEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingUser) return;
    try {
      await updateUser(editingUser.id, { name: editName.trim(), role: editRole, group: editGroup.trim() || 'General', status: editStatus });
      setEditingUser(null);
      setFeedback(`Profile of ${editName.trim()} updated.`);
      if (showAudit) await loadAudit();
    } catch (err: any) {
      setModalError(err?.message || 'Failed to update user.');
    }
  };

  const submitReset = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resetTarget) return;
    if (isConnectedMode && !isValidPassword(resetPassword)) {
      setModalError(PASSWORD_RULE_TEXT);
      return;
    }
    try {
      await resetUserPassword(resetTarget.id, resetPassword);
      setFeedback(`Password reset for ${resetTarget.name}. Any account lockout was cleared.`);
      setResetTarget(null);
      setResetPassword('');
      if (showAudit) await loadAudit();
    } catch (err: any) {
      setModalError(err?.message || 'Failed to reset password.');
    }
  };

  const toggleActive = async (u: UserRecord) => {
    const next = u.status === 'Active' ? 'Inactive' : 'Active';
    try {
      await updateUser(u.id, { name: u.name, role: u.role, group: u.group, status: next });
      setFeedback(`${u.name} is now ${next}.`);
      if (showAudit) await loadAudit();
    } catch (err: any) {
      window.alert(err?.message || 'Failed to change account status.');
    }
  };
  const [searchTerm, setSearchTerm] = useState('');
  const [roleFilter, setRoleFilter] = useState<string>('ALL');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // New user form state
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<UserRecord['role']>('End User');
  const [group, setGroup] = useState('General');
  const [status, setStatus] = useState<'Active' | 'Inactive'>('Active');
  const [selectedAvatar, setSelectedAvatar] = useState(DEFAULT_AVATARS[0]);

  const handleOpenModal = () => {
    setName('');
    setEmail('');
    setPassword('');
    setRole('End User');
    setGroup('General');
    setStatus('Active');
    setSelectedAvatar(DEFAULT_AVATARS[Math.floor(Math.random() * DEFAULT_AVATARS.length)]);
    setErrorMessage(null);
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !email.trim()) {
      setErrorMessage('Full name and email address are required.');
      return;
    }
    if (repositoryManager.getMode() === 'connected' && !isValidPassword(password)) {
      setErrorMessage(`Password rule: ${PASSWORD_RULE_TEXT}`);
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      await addUser({
        name: name.trim(),
        email: email.trim().toLowerCase(),
        role,
        group: group.trim() || 'General',
        status,
        avatarUrl: selectedAvatar,
        password: password || undefined
      });
      setIsModalOpen(false);
    } catch (err: any) {
      setErrorMessage(err?.message || 'Failed to create user. Please verify input.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (id: string, userName: string) => {
    if (window.confirm(`Are you sure you want to remove user "${userName}"?`)) {
      try {
        await deleteUser(id);
      } catch (err: any) {
        window.alert(err?.message || 'Failed to delete user.');
      }
    }
  };

  // Filtered users
  const filteredUsers = (users || []).filter((u) => {
    const matchesSearch = 
      u.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      u.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
      u.group.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesRole = roleFilter === 'ALL' || u.role === roleFilter;

    return matchesSearch && matchesRole;
  });

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Users className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            Users, Permission Groups & Access Management
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Manage BPM roles, Active Directory groups, and approval assignment permissions ({users.length} registered)
          </p>
        </div>

        {canManageUsers && (
        <button 
          onClick={handleOpenModal}
          data-testid="add-user-button"
          className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-md cursor-pointer transition-all active:scale-95"
        >
          <UserPlus className="w-4 h-4" />
          <span>Add User or Group</span>
        </button>
        )}
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-3 p-4 rounded-2xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs">
        <div className="relative w-full sm:w-80">
          <Search className="w-4 h-4 absolute left-3 top-2.5 text-slate-400" />
          <input 
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search by name, email, or department..."
            className="w-full pl-9 pr-3 py-1.5 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-xs text-[#0F172A] dark:text-slate-100 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div className="flex items-center gap-2 w-full sm:w-auto">
          <label className="text-xs font-bold text-slate-500 dark:text-slate-400 whitespace-nowrap">Role:</label>
          <select 
            value={roleFilter}
            onChange={(e) => setRoleFilter(e.target.value)}
            className="bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl px-3 py-1.5 text-xs text-[#0F172A] dark:text-slate-100 font-medium cursor-pointer"
          >
            <option value="ALL">All BPM Roles</option>
            {ALL_ROLES.map(r => (
              <option key={r} value={r}>{r}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Users Table Card */}
      <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs transition-colors">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-xs">
            <thead>
              <tr className="border-b border-[#E2E8F0] dark:border-[#334155] text-[11px] font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
                <th className="pb-3 px-3">User</th>
                <th className="pb-3 px-3">BPM System Role</th>
                <th className="pb-3 px-3">Permission Group</th>
                <th className="pb-3 px-3">Status</th>
                <th className="pb-3 px-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E8F0] dark:divide-[#334155]">
              {filteredUsers.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-400">
                    No users matching the search query or filter.
                  </td>
                </tr>
              ) : (
                filteredUsers.map((u) => (
                  <tr key={u.id} className="hover:bg-[#F8FAFC] dark:hover:bg-slate-800/60 transition-colors">
                    <td className="py-3.5 px-3">
                      <div className="flex items-center gap-2.5">
                        <img 
                          src={u.avatarUrl} 
                          alt={u.name} 
                          className="w-8 h-8 rounded-full object-cover border border-slate-200 dark:border-slate-700" 
                          onError={(e) => {
                            (e.target as HTMLElement).style.display = 'none';
                          }}
                        />
                        <div>
                          <div className="font-extrabold text-[#0F172A] dark:text-white">{u.name}</div>
                          <div className="text-[11px] text-[#64748B] dark:text-slate-400 font-mono">{u.email}</div>
                        </div>
                      </div>
                    </td>

                    <td className="py-3.5 px-3">
                      <span className="font-bold text-[#2563EB] dark:text-blue-400 inline-flex items-center gap-1.5">
                        <Shield className="w-3.5 h-3.5" />
                        <span>{u.role}</span>
                      </span>
                    </td>

                    <td className="py-3.5 px-3 font-medium text-[#0F172A] dark:text-slate-200">
                      <span className="inline-flex items-center gap-1">
                        <Layers className="w-3 h-3 text-slate-400" />
                        {u.group}
                      </span>
                    </td>

                    <td className="py-3.5 px-3">
                      {u.status === 'Active' ? (
                        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-50 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
                          <CheckCircle2 className="w-3 h-3" /> Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400 border border-slate-200 dark:border-slate-700">
                          <XCircle className="w-3 h-3" /> Inactive
                        </span>
                      )}
                    </td>

                    <td className="py-3.5 px-3 text-right">
                      {canManageUsers && (
                      <div className="inline-flex items-center gap-1">
                        <button
                          onClick={() => openEdit(u)}
                          title="Edit user"
                          aria-label={`Edit ${u.name}`}
                          className="p-1.5 text-slate-400 hover:text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-950/40 rounded-lg cursor-pointer transition-colors"
                        >
                          <Pencil className="w-4 h-4" />
                        </button>
                        {isConnectedMode && (
                        <button
                          onClick={() => { setResetTarget(u); setResetPassword(''); setModalError(null); }}
                          title="Reset password"
                          aria-label={`Reset password of ${u.name}`}
                          className="p-1.5 text-slate-400 hover:text-amber-600 hover:bg-amber-50 dark:hover:bg-amber-950/40 rounded-lg cursor-pointer transition-colors"
                        >
                          <KeyRound className="w-4 h-4" />
                        </button>
                        )}
                        {u.role !== 'Administrator' && (
                        <>
                        <button
                          onClick={() => toggleActive(u)}
                          title={u.status === 'Active' ? 'Deactivate account' : 'Reactivate account'}
                          aria-label={`${u.status === 'Active' ? 'Deactivate' : 'Reactivate'} ${u.name}`}
                          className="p-1.5 text-slate-400 hover:text-orange-600 hover:bg-orange-50 dark:hover:bg-orange-950/40 rounded-lg cursor-pointer transition-colors"
                        >
                          <Power className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleDelete(u.id, u.name)}
                          title="Delete user (only possible without pending tasks or instances)"
                          aria-label={`Delete ${u.name}`}
                          className="p-1.5 text-slate-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-950/40 rounded-lg cursor-pointer transition-colors"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                        </>
                        )}
                      </div>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {feedback && (
        <div role="status" className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs font-medium text-emerald-700 dark:text-emerald-300">
          {feedback}
        </div>
      )}

      {/* SSO / OIDC Identity Providers (Admin only) */}
      <AuthProvidersPanel canManage={canManageUsers} />

      {/* Audit trail (principal administrator, connected mode) */}
      {canManageUsers && isConnectedMode && (
        <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs">
          <button
            type="button"
            onClick={async () => { const next = !showAudit; setShowAudit(next); if (next) await loadAudit(); }}
            className="flex items-center gap-2 text-xs font-bold text-[#0F172A] dark:text-white cursor-pointer"
            aria-expanded={showAudit}
          >
            <History className="w-4 h-4 text-[#2563EB]" />
            User administration audit log
          </button>
          {showAudit && (
            <table className="mt-3 w-full text-left border-collapse text-[11px]">
              <thead>
                <tr className="border-b border-[#E2E8F0] dark:border-[#334155] uppercase tracking-wider text-[#64748B] dark:text-slate-400">
                  <th className="pb-2 pe-3">When</th>
                  <th className="pb-2 pe-3">Actor</th>
                  <th className="pb-2 pe-3">Action</th>
                  <th className="pb-2 pe-3">Target</th>
                  <th className="pb-2">Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E2E8F0] dark:divide-[#334155] text-[#0F172A] dark:text-slate-200">
                {auditEntries.length === 0 ? (
                  <tr><td colSpan={5} className="py-4 text-center text-slate-400">No audit entries yet.</td></tr>
                ) : auditEntries.map(a => (
                  <tr key={a.id}>
                    <td className="py-2 pe-3 whitespace-nowrap">{new Date(a.occurredAt).toLocaleString()}</td>
                    <td className="py-2 pe-3 font-mono">{a.actorEmail}</td>
                    <td className="py-2 pe-3 font-bold">{a.action}</td>
                    <td className="py-2 pe-3 font-mono">{a.targetEmail}</td>
                    <td className="py-2">{a.details}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Edit User Modal */}
      {editingUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <form onSubmit={submitEdit} role="dialog" aria-label="Edit user" className="w-full max-w-md bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-3xl shadow-2xl p-6 space-y-4 text-xs">
            <h3 className="font-extrabold text-sm text-[#0F172A] dark:text-white">Edit {editingUser.email}</h3>
            {modalError && <div role="alert" className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-600 font-medium">{modalError}</div>}
            <div>
              <label htmlFor="edit-user-name" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">Full Name</label>
              <input id="edit-user-name" required value={editName} onChange={e => setEditName(e.target.value)}
                className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label htmlFor="edit-user-role" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">BPM Role</label>
                <select id="edit-user-role" value={editRole} onChange={e => setEditRole(e.target.value as Role)}
                  disabled={editingUser.role === 'Administrator'}
                  className="w-full p-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100">
                  {(editingUser.role === 'Administrator' ? ALL_ROLES : ASSIGNABLE_ROLES).map(r => <option key={r} value={r}>{r}</option>)}
                </select>
              </div>
              <div>
                <label htmlFor="edit-user-group" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">Permission Group</label>
                <input id="edit-user-group" value={editGroup} onChange={e => setEditGroup(e.target.value)}
                  className="w-full p-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100" />
              </div>
            </div>
            <div>
              <label htmlFor="edit-user-status" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">Status</label>
              <select id="edit-user-status" value={editStatus} onChange={e => setEditStatus(e.target.value as 'Active' | 'Inactive')}
                disabled={editingUser.role === 'Administrator'}
                className="w-full p-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100">
                <option value="Active">Active</option>
                <option value="Inactive">Inactive (cannot sign in)</option>
              </select>
            </div>
            <div className="flex justify-end gap-2 pt-2 border-t border-[#E2E8F0] dark:border-[#334155]">
              <button type="button" onClick={() => setEditingUser(null)} className="px-3.5 py-1.5 font-bold text-slate-600 dark:text-slate-300 cursor-pointer">Cancel</button>
              <button type="submit" className="px-4 py-1.5 bg-[#2563EB] text-white font-bold rounded-xl cursor-pointer">Save</button>
            </div>
          </form>
        </div>
      )}

      {/* Reset Password Modal */}
      {resetTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <form onSubmit={submitReset} role="dialog" aria-label="Reset password" className="w-full max-w-md bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-3xl shadow-2xl p-6 space-y-4 text-xs">
            <h3 className="font-extrabold text-sm text-[#0F172A] dark:text-white">Reset password for {resetTarget.email}</h3>
            {modalError && <div role="alert" className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-600 font-medium">{modalError}</div>}
            <div>
              <label htmlFor="reset-user-password" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">New password</label>
              <input id="reset-user-password" type="password" autoComplete="new-password" required value={resetPassword}
                onChange={e => setResetPassword(e.target.value)} aria-describedby="reset-password-hint"
                className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100" />
              <p id="reset-password-hint" className="mt-1 text-slate-500">{PASSWORD_RULE_TEXT}</p>
            </div>
            <div className="flex justify-end gap-2 pt-2 border-t border-[#E2E8F0] dark:border-[#334155]">
              <button type="button" onClick={() => setResetTarget(null)} className="px-3.5 py-1.5 font-bold text-slate-600 dark:text-slate-300 cursor-pointer">Cancel</button>
              <button type="submit" className="px-4 py-1.5 bg-amber-600 text-white font-bold rounded-xl cursor-pointer">Reset password</button>
            </div>
          </form>
        </div>
      )}

      {/* Add User Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs animate-in fade-in">
          <div className="w-full max-w-md bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-3xl shadow-2xl p-6 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
              <div className="flex items-center gap-2">
                <UserPlus className="w-5 h-5 text-[#2563EB]" />
                <h3 className="font-extrabold text-sm text-[#0F172A] dark:text-white">Add New BPM User</h3>
              </div>
              <button 
                onClick={handleCloseModal}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="p-3 rounded-xl bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-900 text-xs text-red-600 dark:text-red-400 font-medium">
                {errorMessage}
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-4 text-xs">
              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Full Name</label>
                <div className="relative">
                  <UserCheck className="w-4 h-4 absolute left-3 top-2.5 text-slate-400" />
                  <input
                    type="text"
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="e.g. Walid Mansouri"
                    className="w-full pl-9 pr-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-xs text-[#0F172A] dark:text-slate-100"
                  />
                </div>
              </div>

              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Email Address</label>
                <div className="relative">
                  <Mail className="w-4 h-4 absolute left-3 top-2.5 text-slate-400" />
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="w.mansouri@enterprise.internal"
                    className="w-full pl-9 pr-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-xs text-[#0F172A] dark:text-slate-100"
                  />
                </div>
              </div>

              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Initial Password</label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Min. 8 chars, upper + lower + digit"
                  autoComplete="new-password"
                  className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-xs text-[#0F172A] dark:text-slate-100"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">BPM Role</label>
                  <select
                    value={role}
                    onChange={(e) => setRole(e.target.value as UserRecord['role'])}
                    className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs text-[#0F172A] dark:text-slate-100 font-medium"
                  >
                    {ASSIGNABLE_ROLES.map(r => (
                      <option key={r} value={r}>{r}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Permission Group</label>
                  <input
                    type="text"
                    value={group}
                    onChange={(e) => setGroup(e.target.value)}
                    placeholder="e.g. Finance"
                    className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs text-[#0F172A] dark:text-slate-100"
                  />
                </div>
              </div>

              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">Status</label>
                <div className="flex gap-4">
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input 
                      type="radio" 
                      name="user-status" 
                      value="Active" 
                      checked={status === 'Active'} 
                      onChange={() => setStatus('Active')} 
                    />
                    <span className="font-medium text-emerald-600 dark:text-emerald-400">Active</span>
                  </label>
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input 
                      type="radio" 
                      name="user-status" 
                      value="Inactive" 
                      checked={status === 'Inactive'} 
                      onChange={() => setStatus('Inactive')} 
                    />
                    <span className="font-medium text-slate-500 dark:text-slate-400">Inactive</span>
                  </label>
                </div>
              </div>

              <div>
                <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1.5">Avatar Preset</label>
                <div className="flex items-center gap-2">
                  {DEFAULT_AVATARS.map((avatar, idx) => (
                    <button
                      key={idx}
                      type="button"
                      onClick={() => setSelectedAvatar(avatar)}
                      className={`relative rounded-full p-0.5 cursor-pointer transition-all ${
                        selectedAvatar === avatar ? 'ring-2 ring-blue-600 scale-105' : 'opacity-60 hover:opacity-100'
                      }`}
                    >
                      <img src={avatar} alt="Avatar option" className="w-7 h-7 rounded-full object-cover" />
                    </button>
                  ))}
                </div>
              </div>

              <div className="flex items-center justify-end gap-2 pt-2 border-t border-[#E2E8F0] dark:border-[#334155]">
                <button
                  type="button"
                  onClick={handleCloseModal}
                  className="px-3.5 py-1.5 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl font-bold cursor-pointer transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-4 py-1.5 bg-[#2563EB] hover:bg-blue-700 disabled:opacity-50 text-white font-bold rounded-xl shadow-md cursor-pointer transition-all"
                >
                  {isSubmitting ? 'Creating...' : 'Create User'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
