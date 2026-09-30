import { readFileSync } from 'node:fs';
import { spawn } from 'node:child_process';
const values = Object.fromEntries(readFileSync('.env','utf8').split(/\r?\n/).filter(x=>x && !x.startsWith('#')).map(line=>{
  const i=line.indexOf('='); return [line.slice(0,i),line.slice(i+1)];
}));
const exportOnly = process.argv.includes('--contract');
const child = spawn('dotnet',['run','--project','src/Api','--no-launch-profile', ...(process.argv.includes('--migrate-only') ? ['--','--migrate-only'] : [])], {
  stdio:'inherit', env:{...process.env,
    ASPNETCORE_ENVIRONMENT:'Development', ASPNETCORE_URLS:'http://localhost:8080',
    ConnectionStrings__Database:`Host=localhost;Database=${values.POSTGRES_DB};Username=${values.POSTGRES_USER};Password=${values.POSTGRES_PASSWORD}`,
    Jwt__Key:values.JWT_KEY,
    Database__AutoMigrate:exportOnly ? 'false' : 'true', Seed__Enabled:exportOnly ? 'false' : 'true',
    Seed__AdminEmail:values.ADMIN_EMAIL, Seed__AdminPassword:values.ADMIN_PASSWORD,
    Matching__Enabled:exportOnly ? 'false' : 'true',
  },
});
child.on('error',e=>{console.error(e.message);process.exit(1);});
for (const signal of ['SIGINT','SIGTERM']) process.on(signal,()=>child.kill(signal));
child.on('exit',code=>process.exit(code ?? 1));
