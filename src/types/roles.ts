/**
 * Single source of truth for roles and permissions on the frontend.
 * Mirrors backend Domain/Security/Roles.cs and Permissions.cs - keep both in sync.
 */
export const ROLES = {
  Administrator: 'Administrator',
  BpmAdministrator: 'BPM Administrator',
  ProcessManager: 'Process Manager',
  WorkflowDesigner: 'Workflow Designer',
  WorkflowDeveloper: 'Workflow Developer',
  EndUser: 'End User'
} as const;

export type Role = (typeof ROLES)[keyof typeof ROLES];

export const ALL_ROLES: Role[] = Object.values(ROLES);

/** Roles that can be handed out from the UI ("Administrator" is the unique principal admin). */
export const ASSIGNABLE_ROLES: Role[] = ALL_ROLES.filter(r => r !== ROLES.Administrator);

export const PERMISSIONS = {
  WorkflowsWrite: 'workflows.write',
  DesignsWrite: 'designs.write',
  UsersManage: 'users.manage',
  UsersRead: 'users.read'
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

const ROLE_PERMISSIONS: Record<string, Permission[]> = {
  [ROLES.Administrator]: Object.values(PERMISSIONS),
  [ROLES.BpmAdministrator]: [PERMISSIONS.UsersRead]
};

export function hasPermission(role: string | undefined | null, permission: Permission): boolean {
  return !!role && (ROLE_PERMISSIONS[role] ?? []).includes(permission);
}

export const PASSWORD_RULE_TEXT =
  'At least 8 characters with an uppercase letter, a lowercase letter and a digit.';

export function isValidPassword(password: string): boolean {
  return password.length >= 8 && /[A-Z]/.test(password) && /[a-z]/.test(password) && /\d/.test(password);
}
