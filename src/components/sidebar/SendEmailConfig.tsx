import React from 'react';
import { Mail, Tag, Code } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface SendEmailConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

const VARIABLE_TOKENS = [
  '{EmployeeName}',
  '{Employee.Email}',
  '{Department}',
  '{RequesterEmail}',
  '{Approval.Assignee}',
  '{Approval.Outcome}'
];

export const SendEmailConfig: React.FC<SendEmailConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData } = useWorkflowStore();

  const email = data.sendEmailData || {
    label: 'Send Email',
    to: '',
    subject: '',
    body: '',
    isHtml: false
  };

  const handleInsertToken = (field: 'to' | 'subject' | 'body', token: string) => {
    updateNodeData(nodeId, {
      sendEmailData: {
        ...email,
        [field]: (email[field] || '') + (email[field] ? ' ' : '') + token
      }
    });
  };

  const inputClass = "w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB] shadow-2xs transition-all";

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Task Title / Label</label>
        <input
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Send Welcome Package Email"
        />
      </div>

      <div className="space-y-1.5">
        <div className="flex items-center justify-between">
          <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
            <Mail className="w-3.5 h-3.5 text-[#D97706] dark:text-amber-400" />
            <span>Recipient Email (To)</span>
          </label>
        </div>
        <input
          type="text"
          value={email.to || ''}
          onChange={(e) => updateNodeData(nodeId, {
            sendEmailData: { ...email, to: e.target.value }
          })}
          className={`${inputClass} font-mono`}
          placeholder="{Employee.Email} or hr@company.com"
        />
        <div className="flex flex-wrap gap-1 pt-1">
          <span className="text-[10px] text-[#475569] dark:text-slate-400 font-medium">Insert Token:</span>
          {VARIABLE_TOKENS.slice(0, 3).map((token) => (
            <button
              key={token}
              type="button"
              onClick={() => handleInsertToken('to', token)}
              className="text-[10px] bg-amber-50 dark:bg-amber-950/60 hover:bg-amber-100 dark:hover:bg-amber-900/60 text-[#D97706] dark:text-amber-400 font-mono font-bold px-2 py-0.5 rounded border border-amber-200 dark:border-amber-800 shadow-2xs transition-colors cursor-pointer"
            >
              {token}
            </button>
          ))}
        </div>
      </div>

      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <Tag className="w-3.5 h-3.5 text-[#D97706] dark:text-amber-400" />
          <span>Email Subject Line</span>
        </label>
        <input
          type="text"
          value={email.subject || ''}
          onChange={(e) => updateNodeData(nodeId, {
            sendEmailData: { ...email, subject: e.target.value }
          })}
          className={inputClass}
          placeholder="Welcome to the team, {EmployeeName}!"
        />
      </div>

      <div className="space-y-1.5">
        <div className="flex items-center justify-between">
          <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
            <Code className="w-3.5 h-3.5 text-[#D97706] dark:text-amber-400" />
            <span>Email Body Template</span>
          </label>
          <label className="flex items-center gap-1.5 text-xs text-[#475569] dark:text-slate-300 font-semibold cursor-pointer">
            <input
              type="checkbox"
              checked={email.isHtml ?? false}
              onChange={(e) => updateNodeData(nodeId, {
                sendEmailData: { ...email, isHtml: e.target.checked }
              })}
              className="rounded border-slate-300 dark:border-slate-700 text-[#D97706] focus:ring-[#2563EB]"
            />
            <span>HTML Format</span>
          </label>
        </div>

        <textarea
          rows={5}
          value={email.body || ''}
          onChange={(e) => updateNodeData(nodeId, {
            sendEmailData: { ...email, body: e.target.value }
          })}
          className={`${inputClass} font-sans leading-relaxed`}
          placeholder="Hi {EmployeeName},\n\nWelcome to our team..."
        />

        <div className="space-y-1.5 pt-1">
          <span className="text-[11px] font-bold text-[#475569] dark:text-slate-400">Click Token to Insert into Body:</span>
          <div className="flex flex-wrap gap-1.5">
            {VARIABLE_TOKENS.map((token) => (
              <button
                key={token}
                type="button"
                onClick={() => handleInsertToken('body', token)}
                className="text-[10px] bg-amber-50 dark:bg-amber-950/60 hover:bg-amber-100 dark:hover:bg-amber-900/60 text-[#D97706] dark:text-amber-400 font-mono font-bold px-2.5 py-1 rounded-lg border border-amber-200 dark:border-amber-800 shadow-2xs transition-colors cursor-pointer"
              >
                + {token}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
