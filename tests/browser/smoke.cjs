const {chromium}=require('playwright');
const fs=require('fs'); const assert=require('assert/strict');
const path=require('path');
const {chromiumLaunchOptions,observeBrowserErrors}=require('./browser-support.cjs');
const dir=path.resolve(__dirname, '../../artifacts/browser-tests');
fs.mkdirSync(dir,{recursive:true});
const baseUrl=process.env.WORLDBOX_BASE_URL || 'http://127.0.0.1:8080/SeWZC.SandboxGame/';
(async()=>{
 const browser=await chromium.launch(chromiumLaunchOptions());
 const context=await browser.newContext({viewport:{width:1440,height:960},acceptDownloads:true});
 const page=await context.newPage(); const diagnostics=observeBrowserErrors(page); const errors=diagnostics.errors;
 const read=()=>page.evaluate(async()=>new Promise((resolve,reject)=>{const r=indexedDB.open('sewzc-worldbox',1); r.onsuccess=()=>{const d=r.result;const q=d.transaction('worlds').objectStore('worlds').get('autosave');q.onsuccess=()=>{resolve(q.result);d.close()};q.onerror=()=>reject(q.error)};r.onerror=()=>reject(r.error)}));
 // Fixed 1440×960 viewport: Avalonia draws controls on a canvas.
 // A realistic press duration lets pointer-down and pointer-up cross its event dispatcher.
 const click=async(x,y)=>{await page.mouse.click(x,y,{delay:80});await page.waitForTimeout(600)};
 const save=async()=>{await click(1345,34);await click(720,422);await page.waitForTimeout(800);const raw=await read();assert(raw,'Save did not create IndexedDB record');return JSON.parse(raw)};
 try{
 await page.goto(baseUrl,{waitUntil:'domcontentloaded'});
 await page.waitForFunction(()=>document.querySelector('canvas')?.width>0&&!document.querySelector('.loading'),{},{timeout:60000});
 await page.waitForTimeout(1200);
 await diagnostics.assertHealthy('desktop startup');
 await click(486,902); // Pause before comparing snapshots.
 const before=await save();assert.equal(before.Width,256);assert.equal(before.Nations.length,4);
 console.log('PASS published subpath loads and explicit IndexedDB save',before.Tick,before.Residents.length);
 await click(38,186); await click(740,370); // Human tool and inhabited land.
 const spawned=await save();assert.equal(spawned.Residents.length,before.Residents.length+12);assert.equal(spawned.Tick,before.Tick);
 console.log('PASS resident placement and paused simulation');
 await click(38,136); await click(350,200); // Grass on ocean.
 const painted=await save();assert.equal(painted.Tiles[35*256+45].Terrain,3);assert.notEqual(before.Tiles[35*256+45].Terrain,3);
 console.log('PASS terrain brush edits actual world state');
 await click(38,456);const undone=await save();assert.equal(undone.Residents.length,before.Residents.length);assert.equal(undone.Tiles[35*256+45].Terrain,before.Tiles[35*256+45].Terrain);
 console.log('PASS edit-batch undo restores residents and terrain');
 await click(1345,34);const downloadPromise=page.waitForEvent('download');await click(720,522);const download=await downloadPromise;const exportPath=path.join(dir,'export.json');await download.saveAs(exportPath);
 const exported=JSON.parse(fs.readFileSync(exportPath,'utf8'));assert.equal(exported.Tick,undone.Tick);assert.equal(exported.Residents.length,undone.Residents.length);
 console.log('PASS browser file export');
 exported.Seed=777;fs.writeFileSync(path.join(dir,'import.json'),JSON.stringify(exported));
 await click(1345,34);const filePromise=page.waitForEvent('filechooser');await click(720,572);const chooser=await filePromise;await chooser.setFiles(path.join(dir,'import.json'));await page.waitForTimeout(650);
 const imported=await save();assert.equal(imported.Seed,777);assert.equal(imported.Residents.length,exported.Residents.length);
 console.log('PASS validated browser file import');
 fs.writeFileSync(path.join(dir,'invalid.json'),'{"FormatVersion":999}');await click(1345,34);const invalidPromise=page.waitForEvent('filechooser');await click(720,572);await (await invalidPromise).setFiles(path.join(dir,'invalid.json'));await page.waitForTimeout(450);await page.keyboard.press('Escape');
 const preserved=await save();assert.equal(preserved.Seed,777);assert.equal(preserved.Residents.length,imported.Residents.length);
 console.log('PASS invalid import preserves current world');
 await page.reload({waitUntil:'domcontentloaded'});await page.waitForFunction(()=>document.querySelector('canvas')?.width>0&&!document.querySelector('.loading'),{},{timeout:60000});await page.waitForTimeout(600);await click(486,902);const resumed=await save();assert.equal(resumed.Seed,777);assert(resumed.Tick>=imported.Tick);
 console.log('PASS page reload restores saved world');
 await page.screenshot({path:dir+'/desktop.png'});
 await click(1280,400);await page.waitForTimeout(200);await page.screenshot({path:dir+'/nation-panel.png'});
 await diagnostics.assertHealthy('desktop after reload and interactions');
 fs.writeFileSync(path.join(dir,'desktop-renderer.json'),JSON.stringify({fallbacks:diagnostics.fallbacks,rendererChecks:diagnostics.rendererChecks,errors},null,2));
 assert.deepEqual(errors,[]);console.log('ALL DESKTOP FUNCTIONAL CHECKS PASSED');
 }catch(e){await page.screenshot({path:dir+'/failure.png'});console.error('FAILED',e);process.exitCode=1}
 await browser.close();
})();
