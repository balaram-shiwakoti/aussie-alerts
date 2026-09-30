import { randomBytes } from 'node:crypto';
import { existsSync, writeFileSync } from 'node:fs';
if (existsSync('.env')) { console.log('.env already exists; keeping your settings.'); process.exit(0); }
const env = `POSTGRES_DB=alerts
POSTGRES_USER=alerts
POSTGRES_PASSWORD=${randomBytes(24).toString('hex')}
JWT_KEY=${randomBytes(48).toString('base64')}
ADMIN_EMAIL=admin@example.test
ADMIN_PASSWORD=DemoA1!${randomBytes(18).toString('hex')}
`;
writeFileSync('.env', env, { mode: 0o600 });
console.log('Created .env. Open it locally to find the demo admin password. Never commit this file.');
