import { writeFileSync } from 'node:fs';
const response=await fetch('http://localhost:8080/openapi/v1.json');
if(!response.ok) throw new Error(`OpenAPI export failed: ${response.status}`);
writeFileSync('openapi.json', JSON.stringify(await response.json(),null,2)+'\n');
console.log('Exported openapi.json. Run npm --prefix web run generate:api next.');
