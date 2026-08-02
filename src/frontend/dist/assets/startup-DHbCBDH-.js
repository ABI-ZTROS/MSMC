import{j as e,P as oe,c as ne}from"./globals-CAZhuatY.js";import{r as a,R as ae}from"./vendor-BnmTbN-o.js";import{F as B}from"./IconRegistry-DgOQsezU.js";import"./icons-BChoR3AM.js";let ie=0;function le(n){const i=n.match(/^\s*\[([A-Z0-9_]+)\]/);return i?i[1]:""}function ce(n){switch(n){case"trace":return 0;case"debug":return 1;case"info":return 2;case"warn":return 3;case"error":return 4;case"success":return 5;default:return 2}}const de={BOOT:"#60a5fa",BUILD:"#a78bfa",LOAD:"#fbbf24",OK:"#34d399",ERR:"#f87171",WARN:"#fb923c",TIME:"#22d3ee",DETECT:"#60a5fa",NET:"#38bdf8",SEC:"#f472b6",CFG:"#a78bfa",METRIC:"#4ade80",BASE:"#94a3b8",VM:"#c084fc",FE:"#f59e0b",WV2:"#22d3ee",CLEAN:"#10b981",AUTH:"#ec4899",INIT:"#818cf8",READY:"#34d399",BRIDGE:"#06b6d4",SHUTDOWN:"#ef4444"},pe=`
@keyframes cyberScan {
  0% { transform: translateY(-100%); }
  100% { transform: translateY(100vh); }
}
@keyframes cyberScanV {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100vw); }
}
@keyframes cyberGlitch {
  0%, 100% { clip-path: inset(0 0 0 0); transform: translate(0); }
  20% { clip-path: inset(20% 0 30% 0); transform: translate(-2px, 1px); }
  40% { clip-path: inset(50% 0 10% 0); transform: translate(2px, -1px); }
  60% { clip-path: inset(10% 0 60% 0); transform: translate(-1px, 2px); }
  80% { clip-path: inset(70% 0 5% 0); transform: translate(1px, -2px); }
}
@keyframes cyberGlitchRGB {
  0%, 100% { text-shadow: 0 0 0 transparent; }
  25% { text-shadow: -1px 0 #ff00ff, 1px 0 #00ffff; }
  50% { text-shadow: 2px 0 #ff00ff, -2px 0 #00ffff; }
  75% { text-shadow: -1px 0 #ff00ff, 1px 0 #00ffff; }
}
@keyframes cyberPulse {
  0%, 100% { opacity: 0.4; transform: scale(1); }
  50% { opacity: 0.8; transform: scale(1.05); }
}
@keyframes cyberFlicker {
  0%, 100% { opacity: 1; }
  3% { opacity: 0.4; }
  6% { opacity: 1; }
  7% { opacity: 0.6; }
  8% { opacity: 1; }
  47% { opacity: 1; }
  48% { opacity: 0.3; }
  49% { opacity: 1; }
}
@keyframes cyberBoot {
  0% { opacity: 0; filter: blur(20px); transform: scale(0.8); }
  50% { opacity: 0.5; filter: blur(8px); transform: scale(1.05); }
  100% { opacity: 1; filter: blur(0); transform: scale(1); }
}
@keyframes cyberGridMove {
  0% { background-position: 0 0; }
  100% { background-position: 40px 40px; }
}
@keyframes cyberNeonPulse {
  0%, 100% { box-shadow: 0 0 20px rgba(59,130,246,0.4), 0 0 40px rgba(59,130,246,0.15), inset 0 0 20px rgba(59,130,246,0.08); }
  50% { box-shadow: 0 0 50px rgba(59,130,246,0.7), 0 0 100px rgba(59,130,246,0.3), inset 0 0 35px rgba(59,130,246,0.15); }
}
@keyframes cyberTextGlow {
  0%, 100% { text-shadow: 0 0 10px rgba(96,165,250,0.6), 0 0 20px rgba(96,165,250,0.3); }
  50% { text-shadow: 0 0 25px rgba(96,165,250,0.9), 0 0 50px rgba(96,165,250,0.5); }
}
@keyframes cyberLogEntry {
  from { opacity: 0; transform: translateX(-12px); filter: blur(2px); }
  to { opacity: 1; transform: translateX(0); filter: blur(0); }
}
@keyframes cyberRingRotate {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
@keyframes cyberRingRotateRev {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(-360deg); }
}
@keyframes cyberDataFlow {
  0% { stroke-dashoffset: 0; }
  100% { stroke-dashoffset: -40; }
}
@keyframes cyberCursor {
  0%, 49% { opacity: 1; }
  50%, 100% { opacity: 0; }
}
@keyframes cyberBarFill {
  0% { width: 0%; }
  100% { width: 100%; }
}
@keyframes cyberHexSpin {
  0% { transform: rotate(0deg) scale(1); }
  50% { transform: rotate(180deg) scale(1.08); }
  100% { transform: rotate(360deg) scale(1); }
}
@keyframes cyberNoiseShift {
  0%, 100% { transform: translate(0, 0); }
  25% { transform: translate(-1px, 1px); }
  50% { transform: translate(1px, -1px); }
  75% { transform: translate(-1px, -1px); }
}
@keyframes cyberTagPop {
  0% { transform: scale(0.5) translateY(4px); opacity: 0; }
  60% { transform: scale(1.15) translateY(0); opacity: 1; }
  100% { transform: scale(1) translateY(0); opacity: 1; }
}
@keyframes cyberLevelBar {
  0% { width: 0%; opacity: 0.5; }
  50% { width: 100%; opacity: 1; }
  100% { width: 100%; opacity: 0.8; }
}
@keyframes cyberRipple {
  0% { transform: scale(0.8); opacity: 0.8; }
  100% { transform: scale(2.4); opacity: 0; }
}
@keyframes cyberVignette {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 0.75; }
}
@keyframes cyberHudCorner {
  0%, 100% { opacity: 0.4; }
  50% { opacity: 1; }
}
@keyframes cyberMatrixRain {
  0% { background-position: 0 0; }
  100% { background-position: 0 -200px; }
}
`,fe=String.raw`
███╗   ███╗ ██████╗ ███╗   ██╗ ██████╗     ██╗     ██╗███████╗██████╗
████╗ ████║██╔═══██╗████╗  ██║██╔═══██╗    ██║     ██║██╔════╝██╔══██╗
██╔████╔██║██║   ██║██╔██╗ ██║██║   ██║    ██║     ██║█████╗  ██████╔╝
██║╚██╔╝██║██║   ██║██║╚██╗██║██║   ██║    ██║     ██║██╔══╝  ██╔══██╗
██║ ╚═╝ ██║╚██████╔╝██║ ╚████║╚██████╔╝    ███████╗██║███████╗██║  ██║
╚═╝     ╚═╝ ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝     ╚══════╝╚═╝╚══════╝╚═╝  ╚═╝
`;function me(){const[n,i]=a.useState(0),[f,S]=a.useState("正在初始化..."),[x,N]=a.useState([]),[m,z]=a.useState(!1),[p,A]=a.useState(!1),[P,D]=a.useState("v1.0.0"),[o,T]=a.useState("#3B82F6"),[v,L]=a.useState(!1),[O,k]=a.useState("boot"),[W,U]=a.useState(0),[G,H]=a.useState(60),[M,J]=a.useState(0),[$,Y]=a.useState(0),w=a.useRef(null),_=a.useRef(!1),j=a.useRef(!0),I=a.useRef(Date.now()),V=a.useRef({frames:0,lastTs:performance.now()}),b=a.useCallback((t,s="default")=>{N(l=>[...l.slice(-199),{id:++ie,message:t,type:s,tag:le(t),timestamp:Date.now(),level:ce(s)}])},[]),q=t=>{const s=new Date(t),l=String(s.getHours()).padStart(2,"0"),d=String(s.getMinutes()).padStart(2,"0"),c=String(s.getSeconds()).padStart(2,"0"),u=String(s.getMilliseconds()).padStart(3,"0");return`${l}:${d}:${c}.${u}`},X=t=>{const s=Math.floor(t/60),l=t%60;return`${String(s).padStart(2,"0")}:${String(l).padStart(2,"0")}`};a.useEffect(()=>{function t(){var l;_.current||(l=window.chrome)!=null&&l.webview&&(_.current=!0,window.chrome.webview.addEventListener("message",d=>{const c=d.data;if(!c||!c.type)return;const u=String(c.type).toLowerCase(),h=c.action||"";if(u==="event")switch(h){case"startup:progress":{const r=c.payload;r&&(i(Math.max(0,Math.min(100,r.percent))),typeof r.status=="string"&&r.status.length>0&&S(r.status));break}case"startup:log":{const r=c.payload;if(r){const y=r.isError?"error":r.isSuccess?"success":"default";b(r.message,y)}break}case"startup:completed":{A(!0),i(100),S("初始化完成"),k("done");const r=c.payload,y=(r==null?void 0:r.message)||"[OK] 初始化完成，正在启动主界面...";b(y,"success");break}case"startup:failed":{z(!0),i(100),k("failed");const r=c.payload,y=(r==null?void 0:r.message)||"启动失败";S(`启动失败：${y}`),b(`[ERR] 启动失败：${y}`,"error"),b("[SHUTDOWN] 进入受控关机流程","warn");break}case"startup:init":{const r=c.payload;r!=null&&r.version&&D(`v${r.version}`),r!=null&&r.primaryColor&&T(r.primaryColor),b(`[BRIDGE] 桥接握手完成，版本 ${(r==null?void 0:r.version)||"unknown"}`,"info");break}case"startup:themeChanged":{const r=c.payload;r!=null&&r.primaryColor&&(T(r.primaryColor),b(`[INIT] 主题切换：${r.primaryColor}`,"debug"));break}}}),window.__msmc_bridge__&&window.__msmc_bridge__.sendEvent("startup:ready",{ts:Date.now()}))}document.readyState==="complete"?t():window.addEventListener("load",t,{once:!0}),setTimeout(t,100),setTimeout(t,500),setTimeout(t,1e3);const s=setTimeout(()=>{L(!0),k("running"),b("[BOOT] React 视图挂载完成","success"),b("[BRIDGE] 等待 C# 主机事件 ...","info")},800);return()=>clearTimeout(s)},[b]),a.useEffect(()=>{const t=setInterval(()=>U(Math.floor((Date.now()-I.current)/1e3)),1e3);return()=>clearInterval(t)},[]),a.useEffect(()=>{let t=0;const s=()=>{const l=performance.now(),d=V.current;d.frames++,l-d.lastTs>=1e3&&(H(Math.round(d.frames*1e3/(l-d.lastTs))),d.frames=0,d.lastTs=l,J(Math.round(8+Math.random()*18+(p?0:12))),Y(Math.round(120+Math.random()*60+(p?0:40)))),t=requestAnimationFrame(s)};return t=requestAnimationFrame(s),()=>cancelAnimationFrame(t)},[p]),a.useLayoutEffect(()=>{!w.current||!j.current||requestAnimationFrame(()=>{const s=w.current;s&&s.scrollHeight-s.clientHeight-s.scrollTop<24&&s.scrollTo({top:s.scrollHeight,behavior:"smooth"})})},[x]),a.useEffect(()=>{const t=w.current;if(!t)return;const s=()=>{const l=t.scrollHeight-t.clientHeight-t.scrollTop;j.current=l<24};return t.addEventListener("scroll",s,{passive:!0}),()=>t.removeEventListener("scroll",s)},[]);const Z=()=>{m&&window.__msmc_bridge__?window.__msmc_bridge__.sendEvent("startup:shutdown",{}):window.__msmc_bridge__&&window.__msmc_bridge__.sendEvent("startup:close",{})},K=t=>{t.button===0&&window.__msmc_bridge__&&window.__msmc_bridge__.sendEvent("startup:dragMove",{})},g=m?"#f87171":p?"#34d399":o,R=2*Math.PI*45,Q=R-n/100*R,ee=x.filter(t=>t.type==="error").length,te=x.filter(t=>t.type==="success").length,re=x.filter(t=>t.type==="warn").length;return e.jsxs(e.Fragment,{children:[e.jsx("style",{children:pe}),e.jsxs("div",{className:"w-full h-full flex flex-col min-h-0 relative overflow-hidden",style:{backgroundColor:"#020617",fontFamily:"var(--md-font-family)",color:"var(--md-body)"},children:[e.jsx(oe,{density:1.5,color:o,connect:!0,connectDistance:120,speed:.4,maxOpacity:.5}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,backgroundImage:`
              linear-gradient(rgba(59,130,246,0.06) 1px, transparent 1px),
              linear-gradient(90deg, rgba(59,130,246,0.06) 1px, transparent 1px)
            `,backgroundSize:"40px 40px",animation:"cyberGridMove 4s linear infinite",pointerEvents:"none"}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,opacity:.04,backgroundImage:"repeating-linear-gradient(0deg, rgba(34,197,94,0.5) 0, rgba(34,197,94,0.5) 1px, transparent 1px, transparent 8px)",backgroundSize:"8px 200px",animation:"cyberMatrixRain 8s linear infinite",pointerEvents:"none"}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,background:"repeating-linear-gradient(0deg, transparent 0, transparent 2px, rgba(0,0,0,0.18) 2px, rgba(0,0,0,0.18) 4px)",pointerEvents:"none",zIndex:1}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",left:0,right:0,height:140,background:`linear-gradient(to bottom, transparent, ${o}22, transparent)`,animation:"cyberScan 6s linear infinite",pointerEvents:"none",zIndex:1}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",top:0,bottom:0,width:120,background:`linear-gradient(to right, transparent, ${o}10, transparent)`,animation:"cyberScanV 9s linear infinite",pointerEvents:"none",zIndex:1}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,background:"radial-gradient(ellipse at center, transparent 30%, rgba(2,6,23,0.6) 80%, rgba(2,6,23,0.95) 100%)",pointerEvents:"none",zIndex:2,animation:"cyberVignette 5s ease-in-out infinite"}}),[{top:12,left:12,borderTop:`1px solid ${o}50`,borderLeft:`1px solid ${o}50`},{top:12,right:12,borderTop:`1px solid ${o}50`,borderRight:`1px solid ${o}50`},{bottom:12,left:12,borderBottom:`1px solid ${o}50`,borderLeft:`1px solid ${o}50`},{bottom:12,right:12,borderBottom:`1px solid ${o}50`,borderRight:`1px solid ${o}50`}].map((t,s)=>e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",width:24,height:24,animation:"cyberHudCorner 3s ease-in-out infinite",animationDelay:`${s*.4}s`,pointerEvents:"none",zIndex:3,...t}},s)),e.jsxs("div",{"aria-hidden":!0,style:{position:"absolute",top:24,left:32,fontSize:9,fontFamily:'Consolas, "JetBrains Mono", monospace',color:"var(--md-body-lighter)",opacity:.6,zIndex:5,pointerEvents:"none",letterSpacing:"0.1em"},children:[e.jsx("div",{children:"MSMC://boot.sequence"}),e.jsxs("div",{style:{marginTop:2},children:["SESSION ",I.current.toString(36).toUpperCase()]}),e.jsxs("div",{style:{marginTop:2,color:g},children:["● ",O.toUpperCase()]})]}),e.jsxs("div",{"aria-hidden":!0,style:{position:"absolute",top:24,right:32,fontSize:9,fontFamily:'Consolas, "JetBrains Mono", monospace',color:"var(--md-body-lighter)",opacity:.7,zIndex:5,pointerEvents:"none",textAlign:"right",letterSpacing:"0.1em"},children:[e.jsxs("div",{children:["FPS ",G]}),e.jsxs("div",{style:{marginTop:2,color:M>30?"#fb923c":"#34d399"},children:["CPU ",M,"%"]}),e.jsxs("div",{style:{marginTop:2,color:$>200?"#fb923c":"#60a5fa"},children:["MEM ",$,"MB"]}),e.jsxs("div",{style:{marginTop:2},children:["T+",X(W)]})]}),e.jsxs("div",{className:"flex-1 flex flex-col items-center justify-center px-8 relative",style:{zIndex:10},onMouseDown:K,children:[e.jsxs("div",{className:"relative flex items-center justify-center mb-6",style:{width:160,height:160,animation:v?"none":"cyberBoot 0.8s ease-out forwards"},children:[e.jsx("svg",{width:160,height:160,style:{position:"absolute",animation:"cyberRingRotate 20s linear infinite"},children:Array.from({length:60}).map((t,s)=>{const l=s/60*360,d=s%5===0,c=d?74:78,u=80,h=l*Math.PI/180,r=80+c*Math.cos(h),y=80+c*Math.sin(h),E=80+u*Math.cos(h),se=80+u*Math.sin(h);return e.jsx("line",{x1:r,y1:y,x2:E,y2:se,stroke:`${o}${d?"70":"30"}`,strokeWidth:d?1.4:.7},s)})}),e.jsx("svg",{width:140,height:140,style:{position:"absolute",animation:"cyberRingRotateRev 12s linear infinite"},children:e.jsx("circle",{cx:70,cy:70,r:62,fill:"none",stroke:`${o}40`,strokeWidth:1,strokeDasharray:"3 6"})}),e.jsx("div",{style:{position:"absolute",width:130,height:130,borderRadius:"50%",border:`1px solid ${o}40`,animation:"cyberPulse 2.5s ease-in-out infinite"}}),e.jsx("div",{style:{position:"absolute",width:110,height:110,borderRadius:"50%",border:`1px solid ${o}25`,animation:"cyberPulse 2.5s ease-in-out infinite 0.5s"}}),e.jsxs("svg",{width:120,height:120,style:{position:"absolute",transform:"rotate(-90deg)"},children:[e.jsx("circle",{cx:60,cy:60,r:45,fill:"none",stroke:`${o}15`,strokeWidth:2}),e.jsx("circle",{cx:60,cy:60,r:45,fill:"none",stroke:g,strokeWidth:2.8,strokeLinecap:"round",strokeDasharray:R,strokeDashoffset:Q,style:{transition:"stroke-dashoffset 400ms cubic-bezier(0.33, 1, 0.68, 1)",filter:`drop-shadow(0 0 8px ${g}aa)`}}),e.jsx("circle",{cx:60,cy:60,r:52,fill:"none",stroke:`${o}50`,strokeWidth:1,strokeDasharray:"4 8",style:{animation:"cyberDataFlow 2s linear infinite"}})]}),e.jsxs("div",{style:{width:60,height:60,position:"relative",display:"flex",alignItems:"center",justifyContent:"center",animation:"cyberNeonPulse 2s ease-in-out infinite"},children:[e.jsx("svg",{width:60,height:60,style:{position:"absolute",animation:"cyberHexSpin 8s ease-in-out infinite",filter:`drop-shadow(0 0 12px ${o}80)`},children:e.jsx("polygon",{points:"30,2 56,16 56,44 30,58 4,44 4,16",fill:`linear-gradient(135deg, ${o}, ${o}cc)`,stroke:`${o}`,strokeWidth:1.5})}),e.jsx("div",{style:{position:"absolute",width:60,height:60,clipPath:"polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%)",background:`linear-gradient(135deg, ${o}ee, ${o}88)`,boxShadow:"inset 0 0 12px rgba(255,255,255,0.2)"}}),e.jsx(B,{kind:"ShieldHalvedSolid",size:26,className:"text-white",style:{position:"relative",zIndex:1,filter:"drop-shadow(0 0 4px rgba(255,255,255,0.8))"}})]}),e.jsxs("div",{style:{position:"absolute",bottom:-4,right:-14,fontSize:11,fontFamily:'Consolas, "JetBrains Mono", monospace',color:g,fontWeight:700,textShadow:`0 0 10px ${g}aa`,animation:"cyberFlicker 3s linear infinite",letterSpacing:"0.05em"},children:["[",String(Math.round(n)).padStart(3,"0"),"%]"]}),!p&&!m&&n>0&&e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",width:120,height:120,borderRadius:"50%",border:`2px solid ${o}`,animation:"cyberRipple 1.6s ease-out infinite",pointerEvents:"none"}})]}),e.jsx("div",{className:"text-center mb-1",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.15s both"},children:e.jsxs("div",{style:{fontSize:36,fontWeight:900,letterSpacing:"0.18em",color:"var(--md-body)",position:"relative",display:"inline-block",animation:"cyberTextGlow 3s ease-in-out infinite, cyberGlitchRGB 6s steps(1) infinite"},children:["MSMC",e.jsx("span",{"aria-hidden":!0,style:{position:"absolute",inset:0,color:"#f87171",opacity:.65,animation:"cyberGlitch 4s steps(1) infinite",clipPath:"inset(0 0 0 0)"},children:"MSMC"}),e.jsx("span",{"aria-hidden":!0,style:{position:"absolute",inset:0,color:"#22d3ee",opacity:.55,animation:"cyberGlitch 4s steps(1) infinite 0.2s",clipPath:"inset(0 0 0 0)"},children:"MSMC"})]})}),e.jsxs("div",{className:"text-center mb-5",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.3s both"},children:[e.jsx("div",{style:{fontSize:12,color:"var(--md-body-light)",letterSpacing:"0.12em"},children:"MINECRAFT SERVER MANAGEMENT CONSOLE"}),e.jsxs("div",{style:{fontSize:10,color:"var(--md-body-lighter)",marginTop:6,fontFamily:'Consolas, "JetBrains Mono", monospace',display:"inline-flex",alignItems:"center",gap:8},children:[e.jsx("span",{children:P}),e.jsx("span",{style:{opacity:.4},children:"·"}),e.jsx("span",{style:{padding:"1px 6px",borderRadius:2,fontSize:9,fontWeight:700,letterSpacing:"0.1em",color:m?"#f87171":p?"#34d399":o,border:`1px solid ${m?"#f8717160":p?"#34d39960":`${o}60`}`,background:m?"rgba(248,113,113,0.1)":p?"rgba(52,211,153,0.1)":`${o}10`},children:m?"⚠ SYSTEM ERROR":p?"✓ READY":"▶ BOOTING"})]})]}),e.jsxs("div",{className:"mb-4 flex items-center gap-2",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.45s both"},children:[e.jsx("div",{style:{width:6,height:6,borderRadius:"50%",backgroundColor:g,boxShadow:`0 0 10px ${g}`,animation:!m&&!p?"cyberPulse 1s ease-in-out infinite":"none"}}),e.jsx("span",{style:{fontSize:11,color:"var(--md-body-light)",fontFamily:'Consolas, "JetBrains Mono", monospace',maxWidth:420,overflow:"hidden",whiteSpace:"nowrap",textOverflow:"ellipsis"},children:f}),!m&&!p&&e.jsx("span",{style:{fontSize:11,color:g,fontFamily:"Consolas, monospace",animation:"cyberCursor 1s steps(1) infinite"},children:"▋"})]}),e.jsxs("div",{className:"w-full max-w-[560px] flex flex-col overflow-hidden rounded-xl",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.6s both",height:200,background:"rgba(15, 23, 42, 0.6)",backdropFilter:"blur(14px)",WebkitBackdropFilter:"blur(14px)",border:`1px solid ${o}30`,boxShadow:`0 4px 24px rgba(0,0,0,0.5), 0 0 60px ${o}10, inset 0 1px 0 rgba(255,255,255,0.06)`},children:[e.jsxs("div",{className:"flex items-center justify-between px-3 py-1.5 flex-shrink-0",style:{borderBottom:`1px solid ${o}20`,background:"rgba(2, 6, 23, 0.4)"},children:[e.jsxs("div",{className:"flex items-center gap-2",children:[e.jsxs("div",{style:{display:"flex",gap:5},children:[e.jsx("div",{style:{width:9,height:9,borderRadius:"50%",background:"#f87171",boxShadow:"0 0 4px rgba(248,113,113,0.5)"}}),e.jsx("div",{style:{width:9,height:9,borderRadius:"50%",background:"#fbbf24",boxShadow:"0 0 4px rgba(251,191,36,0.5)"}}),e.jsx("div",{style:{width:9,height:9,borderRadius:"50%",background:"#34d399",boxShadow:"0 0 4px rgba(52,211,153,0.5)"}})]}),e.jsx("div",{style:{width:8}}),e.jsx(B,{kind:"TerminalSolid",size:12,style:{color:o}}),e.jsx("span",{style:{fontSize:10,fontWeight:700,color:"var(--md-body-light)",letterSpacing:"0.1em"},children:"SYSTEM CONSOLE / tty/msmc0"})]}),e.jsxs("div",{className:"flex items-center gap-3",children:[e.jsxs("div",{style:{display:"flex",gap:8,fontFamily:"Consolas, monospace",fontSize:9},children:[e.jsxs("span",{style:{color:"#f87171"},children:["E:",ee]}),e.jsxs("span",{style:{color:"#fb923c"},children:["W:",re]}),e.jsxs("span",{style:{color:"#34d399"},children:["S:",te]}),e.jsxs("span",{style:{color:"var(--md-body-lighter)"},children:["N:",x.length]})]}),e.jsxs("span",{style:{fontSize:9,color:"var(--md-body-lighter)",fontFamily:"Consolas, monospace"},children:[x.length," entries"]}),e.jsx("div",{style:{width:6,height:6,borderRadius:"50%",backgroundColor:g,boxShadow:`0 0 8px ${g}`,animation:!m&&!p?"cyberPulse 1.5s ease-in-out infinite":"none"}})]})]}),e.jsxs("div",{ref:w,className:"flex-1 overflow-y-auto min-h-0",style:{padding:"8px 12px",scrollbarWidth:"thin"},children:[x.length===0&&e.jsxs("div",{style:{fontSize:11,color:"var(--md-body-lighter)",opacity:.4,textAlign:"center",padding:"20px 0",fontFamily:'Consolas, "JetBrains Mono", monospace'},children:["awaiting system signals",e.jsx("span",{style:{animation:"cyberCursor 1s steps(1) infinite"},children:"▋"})]}),x.map(t=>{const s=de[t.tag]||"var(--md-body)",l=t.type==="error",d=t.type==="success",c=t.type==="warn",u=t.type==="info"||t.type==="debug"||t.type==="trace",h=t.message.match(/^(\s*\[[A-Z0-9_]+\])(.*)$/s),r=h?h[1]:"",y=h?h[2]:t.message,E=l?"✗":d?"✓":c?"!":u?"›":"·";return e.jsxs("div",{style:{display:"flex",gap:6,alignItems:"flex-start",fontFamily:'Consolas, "JetBrains Mono", "Cascadia Code", monospace',fontSize:11,lineHeight:1.7,marginBottom:1,padding:"1px 4px",borderRadius:2,animation:"cyberLogEntry 0.25s ease-out",backgroundColor:l?"rgba(239, 68, 68, 0.10)":d?"rgba(52, 211, 153, 0.07)":c?"rgba(251, 146, 60, 0.07)":"transparent",color:l?"#f87171":d?"#34d399":c?"#fb923c":"var(--md-body)",wordBreak:"break-word",whiteSpace:"pre-wrap",borderLeft:r?`2px solid ${s}`:"none",paddingLeft:r?6:4},children:[e.jsx("span",{style:{flexShrink:0,fontSize:9,color:"var(--md-body-lighter)",opacity:.5,userSelect:"none"},children:q(t.timestamp)}),e.jsx("span",{style:{flexShrink:0,color:l?"#f87171":d?"#34d399":c?"#fb923c":s,fontWeight:700,fontSize:11,userSelect:"none",width:10,textAlign:"center"},children:E}),r&&e.jsx("span",{style:{flexShrink:0,color:s,fontWeight:700,fontSize:10,userSelect:"none",animation:"cyberTagPop 0.3s ease-out"},children:r}),e.jsx("span",{style:{flex:1},children:y})]},t.id)})]}),e.jsxs("div",{className:"flex-shrink-0 px-3 py-1",style:{borderTop:`1px solid ${o}15`,background:"rgba(2, 6, 23, 0.5)",fontFamily:'Consolas, "JetBrains Mono", monospace',fontSize:9,color:"var(--md-body-lighter)",display:"flex",justifyContent:"space-between"},children:[e.jsx("span",{children:"root@msmc:~#"}),e.jsxs("span",{style:{opacity:.6},children:[j.current?"TAIL":"PAUSED"," · UTF-8 · LF"]})]})]}),e.jsx("div",{className:"mt-5",style:{height:40},children:m&&e.jsx("button",{onClick:Z,className:"px-6 py-2 text-white font-semibold rounded cursor-pointer border-none",style:{width:140,height:38,fontSize:12,letterSpacing:"0.15em",background:"linear-gradient(135deg, #dc2626, #991b1b)",boxShadow:"0 0 24px rgba(220,38,38,0.5), inset 0 1px 0 rgba(255,255,255,0.15)",transition:"all 150ms ease"},onMouseEnter:t=>{t.currentTarget.style.boxShadow="0 0 36px rgba(220,38,38,0.7), inset 0 1px 0 rgba(255,255,255,0.2)",t.currentTarget.style.transform="scale(1.04)"},onMouseLeave:t=>{t.currentTarget.style.boxShadow="0 0 24px rgba(220,38,38,0.5), inset 0 1px 0 rgba(255,255,255,0.15)",t.currentTarget.style.transform="scale(1)"},children:"⏻ SHUTDOWN"})})]}),e.jsxs("div",{className:"text-center pb-3 flex-shrink-0",style:{zIndex:10,pointerEvents:"none"},children:[e.jsx("div",{style:{fontSize:8,color:"var(--md-body-lighter)",opacity:.25,fontFamily:'Consolas, "JetBrains Mono", monospace',letterSpacing:"0.15em",whiteSpace:"pre",lineHeight:1.1,display:"none"},children:fe}),e.jsx("span",{style:{fontSize:9,color:"var(--md-body-lighter)",opacity:.35,fontFamily:'Consolas, "JetBrains Mono", monospace',letterSpacing:"0.2em"},children:"io.NET.ZTR_OS · SECURED · UTC+8 · © 2026 ABI-ZTROS"})]})]})]})}window.__msmcStartupScriptLoaded=!0;function C(n,i,f){try{const S=window.__msmc_bridge__;S&&typeof S.invoke=="function"&&S.invoke("log:write",{level:n,message:i,stack:f||"",url:location.href,ua:navigator.userAgent}).catch(()=>{})}catch{}}window.addEventListener("error",n=>{var f;const i=(n.message||"未知错误")+(n.filename?` @ ${n.filename}:${n.lineno||0}:${n.colno||0}`:"");console.error("[STARTUP-ERR]",i,n.error),C("Error",`[STARTUP-ERR] ${i}`,(f=n.error)==null?void 0:f.stack)});window.addEventListener("unhandledrejection",n=>{const i=n.reason,f=i&&(i.message||i.toString())||"未处理的 Promise 拒绝";console.error("[STARTUP-ERR] Unhandled rejection:",i),C("Error",`[STARTUP-ERR] 未处理的 Promise 拒绝: ${f}`,i==null?void 0:i.stack)});const F=document.getElementById("root");if(F)try{ne.createRoot(F).render(e.jsx(ae.StrictMode,{children:e.jsx(me,{})}));const n=document.getElementById("boot-diagnostics");n&&n.parentNode&&n.parentNode.removeChild(n)}catch(n){const i=n instanceof Error?n.stack:String(n);C("Error",`[STARTUP-ERR] React 渲染异常: ${String(n)}`,i);const f=document.getElementById("boot-log");f&&(f.textContent+=`[FATAL] React 渲染失败: ${String(n)}
${i||""}
`)}
