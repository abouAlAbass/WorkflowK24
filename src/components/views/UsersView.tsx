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
  Layers 
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import type { UserRecord } from '../../types/workflow';

const DEFAULT_AVATARS = [
  '/avatars/user-2.svg',
  '/avatars/user-3.svg',
  '/avatars/user-5.svg',
  '/avatars/user-1.svg',
  '/avatars/user-4.svg'
];

export const UsersView: React.FC = () => {
  const { users, addUser, deleteUser } = useWorkflowStore();

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

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      await addUser({
        name: name.trim(),
        email: email.trim().toLowerCase(),
        role,
        group: group.trim() || 'General',
        status,
        avatarUrl: selectedAvatar
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
      } catch (err) {
        console.error('Failed to delete user:', err);
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

        <button 
          onClick={handleOpenModal}
          data-testid="add-user-button"
          className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-md cursor-pointer transition-all active:scale-95"
        >
          <UserPlus className="w-4 h-4" />
          <span>Add User or Group</span>
        </button>
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
            <option value="BPM Administrator">BPM Administrator</option>
            <option value="Process Manager">Process Manager</option>
            <option value="Workflow Developer">Workflow Developer</option>
            <option value="End User">End User</option>
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
                      <button
                        onClick={() => handleDelete(u.id, u.name)}
                        title="Delete user"
                        className="p-1.5 text-slate-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-950/40 rounded-lg cursor-pointer transition-colors"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

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

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">BPM Role</label>
                  <select
                    value={role}
                    onChange={(e) => setRole(e.target.value as UserRecord['role'])}
                    className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs text-[#0F172A] dark:text-slate-100 font-medium"
                  >
                    <option value="End User">End User</option>
                    <option value="Workflow Developer">Workflow Developer</option>
                    <option value="Process Manager">Process Manager</option>
                    <option value="BPM Administrator">BPM Administrator</option>
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
