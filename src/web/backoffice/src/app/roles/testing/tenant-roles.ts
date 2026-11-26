import { RoleList } from '../roles-api';

export const tenantRoles: RoleList = {
  roles: [
    {
      name: 'identity.administrator',
      displayName: 'Administrator',
      permissions: [
        { name: 'identity.roles.assign', displayName: 'Assign roles' },
        { name: 'identity.users.manage', displayName: 'Manage users' },
        { name: 'identity.roles.view', displayName: 'View roles' },
        { name: 'identity.users.view', displayName: 'View users' },
      ],
    },
    {
      name: 'identity.viewer',
      displayName: 'Viewer',
      permissions: [
        { name: 'identity.roles.view', displayName: 'View roles' },
        { name: 'identity.users.view', displayName: 'View users' },
      ],
    },
  ],
  canAssign: true,
};
