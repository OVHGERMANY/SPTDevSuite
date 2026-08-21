namespace SPTDevSuite.Server.Web;

public static class DashboardPage
{
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>SPTDevSuite</title>
          <style>
            :root { color-scheme: dark; font: 15px system-ui,sans-serif; background:#111827; color:#e5e7eb; }
            body { margin:0; display:grid; grid-template-columns:220px 1fr; min-height:100vh; }
            aside { padding:22px 14px; background:#0b1220; border-right:1px solid #263244; }
            h1 { font-size:18px; margin:0 8px 20px; }
            nav button { width:100%; padding:9px 12px; margin:2px 0; text-align:left; color:inherit; background:transparent; border:0; border-radius:6px; cursor:pointer; }
            nav button:hover, nav button.active { background:#1f2d42; }
            main { padding:28px; max-width:1200px; }
            .card { background:#172033; border:1px solid #2b3950; border-radius:9px; padding:18px; margin-bottom:16px; }
            input { color:inherit; background:#0f1727; border:1px solid #3a4a64; border-radius:5px; padding:8px; min-width:340px; }
            table { width:100%; border-collapse:collapse; margin-top:14px; }
            th,td { padding:8px; border-bottom:1px solid #2b3950; text-align:left; vertical-align:top; }
            code { color:#9bd5ff; } .muted { color:#9ca3af; } .error { color:#fca5a5; }
          </style>
        </head>
        <body>
          <aside><h1>SPTDevSuite</h1><nav id="tabs"></nav></aside>
          <main><h2 id="title">Overview</h2><div id="content" class="card">Loading…</div></main>
          <script>
            'use strict';
            const names=['Overview','Items','Profile','Unlocks','Traders','Quests','Skills','Hideout','Raids','Backups','Settings'];
            const implemented=new Set(['Overview','Items','Profile','Settings']);
            const tabs=document.getElementById('tabs'), content=document.getElementById('content'), title=document.getElementById('title');
            for (const name of names) { const b=document.createElement('button'); b.textContent=name; b.onclick=()=>openTab(name,b); tabs.appendChild(b); }
            async function api(path) { const r=await fetch('/devsuite/api/'+path,{credentials:'same-origin',headers:{'Accept':'application/json'}}); if(!r.ok) throw new Error(await r.text()||('HTTP '+r.status)); return r.json(); }
            function clear() { content.replaceChildren(); }
            function pre(value) { clear(); const p=document.createElement('pre'); p.textContent=JSON.stringify(value,null,2); content.appendChild(p); }
            async function openTab(name,button) {
              document.querySelectorAll('nav button').forEach(x=>x.classList.remove('active')); button.classList.add('active'); title.textContent=name; clear();
              if(!implemented.has(name)) { content.textContent='Not implemented in this foundation milestone'; return; }
              try {
                if(name==='Overview') pre(await api('overview'));
                else if(name==='Profile') pre(await api('profile'));
                else if(name==='Settings') pre(await api('settings'));
                else await showItems();
              } catch(e) { content.textContent=e.message; content.className='card error'; }
            }
            async function showItems() {
              clear(); const input=document.createElement('input'); input.placeholder='Search name, short name, tag, or exact template ID';
              const table=document.createElement('table'); content.append(input,table);
              async function run() {
                const data=await api('items?text='+encodeURIComponent(input.value)+'&page=1&pageSize=50'); table.replaceChildren();
                const head=document.createElement('tr'); for(const h of ['Template ID','Name','Type','Caliber','Tags']) { const th=document.createElement('th'); th.textContent=h; head.appendChild(th); } table.appendChild(head);
                for(const item of data.items) { const row=document.createElement('tr'); for(const value of [item.templateId,item.displayName,item.itemType,item.ammunitionCaliber||item.weaponCaliber||'',item.tags.join(', ')]) { const td=document.createElement('td'); td.textContent=value; row.appendChild(td); } table.appendChild(row); }
              }
              input.addEventListener('input',()=>{ clearTimeout(input.timer); input.timer=setTimeout(()=>run().catch(e=>{table.textContent=e.message;}),180); }); await run();
            }
            openTab('Overview',tabs.firstChild);
          </script>
        </body>
        </html>
        """;
}
