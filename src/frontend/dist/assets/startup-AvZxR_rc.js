import{j as e,P as oe,c as ne}from"./globals--pUJS3o1.js";import{r as i}from"./vendor-BeY2h1CR.js";import{F as I}from"./IconRegistry-Dm1IHTMd.js";import"./icons-ZWRwFCkI.js";let ae=0;function ie(o){const a=o.match(/^\s*\[([A-Z0-9_]+)\]/);return a?a[1]:""}function le(o){switch(o){case"trace":return 0;case"debug":return 1;case"info":return 2;case"warn":return 3;case"error":return 4;case"success":return 5;default:return 2}}const ce={BOOT:"#60a5fa",BUILD:"#a78bfa",LOAD:"#fbbf24",OK:"#34d399",ERR:"#f87171",WARN:"#fb923c",TIME:"#22d3ee",DETECT:"#60a5fa",NET:"#38bdf8",SEC:"#f472b6",CFG:"#a78bfa",METRIC:"#4ade80",BASE:"#94a3b8",VM:"#c084fc",FE:"#f59e0b",WV2:"#22d3ee",CLEAN:"#10b981",AUTH:"#ec4899",INIT:"#818cf8",READY:"#34d399",BRIDGE:"#06b6d4",SHUTDOWN:"#ef4444"},de=`
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
`,pe=String.raw`
███╗   ███╗ ██████╗ ███╗   ██╗ ██████╗     ██╗     ██╗███████╗██████╗
████╗ ████║██╔═══██╗████╗  ██║██╔═══██╗    ██║     ██║██╔════╝██╔══██╗
██╔████╔██║██║   ██║██╔██╗ ██║██║   ██║    ██║     ██║█████╗  ██████╔╝
██║╚██╔╝██║██║   ██║██║╚██╗██║██║   ██║    ██║     ██║██╔══╝  ██╔══██╗
██║ ╚═╝ ██║╚██████╔╝██║ ╚████║╚██████╔╝    ███████╗██║███████╗██║  ██║
╚═╝     ╚═╝ ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝     ╚══════╝╚═╝╚══════╝╚═╝  ╚═╝
`;function fe(){const[o,a]=i.useState(0),[p,S]=i.useState("正在初始化..."),[b,N]=i.useState([]),[m,z]=i.useState(!1),[f,A]=i.useState(!1),[L,P]=i.useState("v1.0.0"),[n,T]=i.useState("#3B82F6"),[v,D]=i.useState(!1),[O,k]=i.useState("boot"),[W,U]=i.useState(0),[G,H]=i.useState(60),[M,J]=i.useState(0),[$,Y]=i.useState(0),w=i.useRef(null),_=i.useRef(!1),j=i.useRef(!0),F=i.useRef(Date.now()),V=i.useRef({frames:0,lastTs:performance.now()}),y=i.useCallback((t,s="default")=>{N(l=>[...l.slice(-199),{id:++ae,message:t,type:s,tag:ie(t),timestamp:Date.now(),level:le(s)}])},[]),q=t=>{const s=new Date(t),l=String(s.getHours()).padStart(2,"0"),d=String(s.getMinutes()).padStart(2,"0"),c=String(s.getSeconds()).padStart(2,"0"),x=String(s.getMilliseconds()).padStart(3,"0");return`${l}:${d}:${c}.${x}`},X=t=>{const s=Math.floor(t/60),l=t%60;return`${String(s).padStart(2,"0")}:${String(l).padStart(2,"0")}`};i.useEffect(()=>{function t(){var l;_.current||(l=window.chrome)!=null&&l.webview&&(_.current=!0,window.chrome.webview.addEventListener("message",d=>{const c=d.data;if(!c||!c.type)return;const x=String(c.type).toLowerCase(),h=c.action||"";if(x==="event")switch(h){case"startup:progress":{const r=c.payload;r&&(a(Math.max(0,Math.min(100,r.percent))),typeof r.status=="string"&&r.status.length>0&&S(r.status));break}case"startup:log":{const r=c.payload;if(r){const u=r.isError?"error":r.isSuccess?"success":"default";y(r.message,u)}break}case"startup:completed":{A(!0),a(100),S("初始化完成"),k("done");const r=c.payload,u=(r==null?void 0:r.message)||"[OK] 初始化完成，正在启动主界面...";y(u,"success");break}case"startup:failed":{z(!0),a(100),k("failed");const r=c.payload,u=(r==null?void 0:r.message)||"启动失败";S(`启动失败：${u}`),y(`[ERR] 启动失败：${u}`,"error"),y("[SHUTDOWN] 进入受控关机流程","warn");break}case"startup:init":{const r=c.payload;r!=null&&r.version&&P(`v${r.version}`),r!=null&&r.primaryColor&&T(r.primaryColor),y(`[BRIDGE] 桥接握手完成，版本 ${(r==null?void 0:r.version)||"unknown"}`,"info");break}case"startup:themeChanged":{const r=c.payload;r!=null&&r.primaryColor&&(T(r.primaryColor),y(`[INIT] 主题切换：${r.primaryColor}`,"debug"));break}}}),window.__msmc_bridge__&&window.__msmc_bridge__.sendEvent("startup:ready",{ts:Date.now()}))}document.readyState==="complete"?t():window.addEventListener("load",t,{once:!0}),setTimeout(t,100),setTimeout(t,500),setTimeout(t,1e3);const s=setTimeout(()=>{D(!0),k("running"),y("[BOOT] React 视图挂载完成","success"),y("[BRIDGE] 等待 C# 主机事件 ...","info")},800);return()=>clearTimeout(s)},[y]),i.useEffect(()=>{const t=setInterval(()=>U(Math.floor((Date.now()-F.current)/1e3)),1e3);return()=>clearInterval(t)},[]),i.useEffect(()=>{let t=0;const s=()=>{const l=performance.now(),d=V.current;d.frames++,l-d.lastTs>=1e3&&(H(Math.round(d.frames*1e3/(l-d.lastTs))),d.frames=0,d.lastTs=l,J(Math.round(8+Math.random()*18+(f?0:12))),Y(Math.round(120+Math.random()*60+(f?0:40)))),t=requestAnimationFrame(s)};return t=requestAnimationFrame(s),()=>cancelAnimationFrame(t)},[f]),i.useLayoutEffect(()=>{!w.current||!j.current||requestAnimationFrame(()=>{const s=w.current;s&&s.scrollHeight-s.clientHeight-s.scrollTop<24&&s.scrollTo({top:s.scrollHeight,behavior:"smooth"})})},[b]),i.useEffect(()=>{const t=w.current;if(!t)return;const s=()=>{const l=t.scrollHeight-t.clientHeight-t.scrollTop;j.current=l<24};return t.addEventListener("scroll",s,{passive:!0}),()=>t.removeEventListener("scroll",s)},[]);const Z=()=>{m&&window.__msmc_bridge__?window.__msmc_bridge__.sendEvent("startup:shutdown",{}):window.__msmc_bridge__&&window.__msmc_bridge__.sendEvent("startup:close",{})},K=t=>{t.button===0&&window.__msmc_bridge__&&window.__msmc_bridge__.sendEvent("startup:dragMove",{})},g=m?"#f87171":f?"#34d399":n,R=2*Math.PI*45,Q=R-o/100*R,ee=b.filter(t=>t.type==="error").length,te=b.filter(t=>t.type==="success").length,re=b.filter(t=>t.type==="warn").length;return e.jsxs(e.Fragment,{children:[e.jsx("style",{children:de}),e.jsxs("div",{className:"w-full h-full flex flex-col min-h-0 relative overflow-hidden",style:{backgroundColor:"#020617",fontFamily:"var(--md-font-family)",color:"var(--md-body)"},children:[e.jsx(oe,{density:1.5,color:n,connect:!0,connectDistance:120,speed:.4,maxOpacity:.5}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,backgroundImage:`
              linear-gradient(rgba(59,130,246,0.06) 1px, transparent 1px),
              linear-gradient(90deg, rgba(59,130,246,0.06) 1px, transparent 1px)
            `,backgroundSize:"40px 40px",animation:"cyberGridMove 4s linear infinite",pointerEvents:"none"}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,opacity:.04,backgroundImage:"repeating-linear-gradient(0deg, rgba(34,197,94,0.5) 0, rgba(34,197,94,0.5) 1px, transparent 1px, transparent 8px)",backgroundSize:"8px 200px",animation:"cyberMatrixRain 8s linear infinite",pointerEvents:"none"}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,background:"repeating-linear-gradient(0deg, transparent 0, transparent 2px, rgba(0,0,0,0.18) 2px, rgba(0,0,0,0.18) 4px)",pointerEvents:"none",zIndex:1}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",left:0,right:0,height:140,background:`linear-gradient(to bottom, transparent, ${n}22, transparent)`,animation:"cyberScan 6s linear infinite",pointerEvents:"none",zIndex:1}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",top:0,bottom:0,width:120,background:`linear-gradient(to right, transparent, ${n}10, transparent)`,animation:"cyberScanV 9s linear infinite",pointerEvents:"none",zIndex:1}}),e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",inset:0,background:"radial-gradient(ellipse at center, transparent 30%, rgba(2,6,23,0.6) 80%, rgba(2,6,23,0.95) 100%)",pointerEvents:"none",zIndex:2,animation:"cyberVignette 5s ease-in-out infinite"}}),[{top:12,left:12,borderTop:`1px solid ${n}50`,borderLeft:`1px solid ${n}50`},{top:12,right:12,borderTop:`1px solid ${n}50`,borderRight:`1px solid ${n}50`},{bottom:12,left:12,borderBottom:`1px solid ${n}50`,borderLeft:`1px solid ${n}50`},{bottom:12,right:12,borderBottom:`1px solid ${n}50`,borderRight:`1px solid ${n}50`}].map((t,s)=>e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",width:24,height:24,animation:"cyberHudCorner 3s ease-in-out infinite",animationDelay:`${s*.4}s`,pointerEvents:"none",zIndex:3,...t}},s)),e.jsxs("div",{"aria-hidden":!0,style:{position:"absolute",top:24,left:32,fontSize:9,fontFamily:'Consolas, "JetBrains Mono", monospace',color:"var(--md-body-lighter)",opacity:.6,zIndex:5,pointerEvents:"none",letterSpacing:"0.1em"},children:[e.jsx("div",{children:"MSMC://boot.sequence"}),e.jsxs("div",{style:{marginTop:2},children:["SESSION ",F.current.toString(36).toUpperCase()]}),e.jsxs("div",{style:{marginTop:2,color:g},children:["● ",O.toUpperCase()]})]}),e.jsxs("div",{"aria-hidden":!0,style:{position:"absolute",top:24,right:32,fontSize:9,fontFamily:'Consolas, "JetBrains Mono", monospace',color:"var(--md-body-lighter)",opacity:.7,zIndex:5,pointerEvents:"none",textAlign:"right",letterSpacing:"0.1em"},children:[e.jsxs("div",{children:["FPS ",G]}),e.jsxs("div",{style:{marginTop:2,color:M>30?"#fb923c":"#34d399"},children:["CPU ",M,"%"]}),e.jsxs("div",{style:{marginTop:2,color:$>200?"#fb923c":"#60a5fa"},children:["MEM ",$,"MB"]}),e.jsxs("div",{style:{marginTop:2},children:["T+",X(W)]})]}),e.jsxs("div",{className:"flex-1 flex flex-col items-center justify-center px-8 relative",style:{zIndex:10},onMouseDown:K,children:[e.jsxs("div",{className:"relative flex items-center justify-center mb-6",style:{width:160,height:160,animation:v?"none":"cyberBoot 0.8s ease-out forwards"},children:[e.jsx("svg",{width:160,height:160,style:{position:"absolute",animation:"cyberRingRotate 20s linear infinite"},children:Array.from({length:60}).map((t,s)=>{const l=s/60*360,d=s%5===0,c=d?74:78,x=80,h=l*Math.PI/180,r=80+c*Math.cos(h),u=80+c*Math.sin(h),E=80+x*Math.cos(h),se=80+x*Math.sin(h);return e.jsx("line",{x1:r,y1:u,x2:E,y2:se,stroke:`${n}${d?"70":"30"}`,strokeWidth:d?1.4:.7},s)})}),e.jsx("svg",{width:140,height:140,style:{position:"absolute",animation:"cyberRingRotateRev 12s linear infinite"},children:e.jsx("circle",{cx:70,cy:70,r:62,fill:"none",stroke:`${n}40`,strokeWidth:1,strokeDasharray:"3 6"})}),e.jsx("div",{style:{position:"absolute",width:130,height:130,borderRadius:"50%",border:`1px solid ${n}40`,animation:"cyberPulse 2.5s ease-in-out infinite"}}),e.jsx("div",{style:{position:"absolute",width:110,height:110,borderRadius:"50%",border:`1px solid ${n}25`,animation:"cyberPulse 2.5s ease-in-out infinite 0.5s"}}),e.jsxs("svg",{width:120,height:120,style:{position:"absolute",transform:"rotate(-90deg)"},children:[e.jsx("circle",{cx:60,cy:60,r:45,fill:"none",stroke:`${n}15`,strokeWidth:2}),e.jsx("circle",{cx:60,cy:60,r:45,fill:"none",stroke:g,strokeWidth:2.8,strokeLinecap:"round",strokeDasharray:R,strokeDashoffset:Q,style:{transition:"stroke-dashoffset 400ms cubic-bezier(0.33, 1, 0.68, 1)",filter:`drop-shadow(0 0 8px ${g}aa)`}}),e.jsx("circle",{cx:60,cy:60,r:52,fill:"none",stroke:`${n}50`,strokeWidth:1,strokeDasharray:"4 8",style:{animation:"cyberDataFlow 2s linear infinite"}})]}),e.jsxs("div",{style:{width:60,height:60,position:"relative",display:"flex",alignItems:"center",justifyContent:"center",animation:"cyberNeonPulse 2s ease-in-out infinite"},children:[e.jsx("svg",{width:60,height:60,style:{position:"absolute",animation:"cyberHexSpin 8s ease-in-out infinite",filter:`drop-shadow(0 0 12px ${n}80)`},children:e.jsx("polygon",{points:"30,2 56,16 56,44 30,58 4,44 4,16",fill:`linear-gradient(135deg, ${n}, ${n}cc)`,stroke:`${n}`,strokeWidth:1.5})}),e.jsx("div",{style:{position:"absolute",width:60,height:60,clipPath:"polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%)",background:`linear-gradient(135deg, ${n}ee, ${n}88)`,boxShadow:"inset 0 0 12px rgba(255,255,255,0.2)"}}),e.jsx(I,{kind:"ShieldHalvedSolid",size:26,className:"text-white",style:{position:"relative",zIndex:1,filter:"drop-shadow(0 0 4px rgba(255,255,255,0.8))"}})]}),e.jsxs("div",{style:{position:"absolute",bottom:-4,right:-14,fontSize:11,fontFamily:'Consolas, "JetBrains Mono", monospace',color:g,fontWeight:700,textShadow:`0 0 10px ${g}aa`,animation:"cyberFlicker 3s linear infinite",letterSpacing:"0.05em"},children:["[",String(Math.round(o)).padStart(3,"0"),"%]"]}),!f&&!m&&o>0&&e.jsx("div",{"aria-hidden":!0,style:{position:"absolute",width:120,height:120,borderRadius:"50%",border:`2px solid ${n}`,animation:"cyberRipple 1.6s ease-out infinite",pointerEvents:"none"}})]}),e.jsx("div",{className:"text-center mb-1",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.15s both"},children:e.jsxs("div",{style:{fontSize:36,fontWeight:900,letterSpacing:"0.18em",color:"var(--md-body)",position:"relative",display:"inline-block",animation:"cyberTextGlow 3s ease-in-out infinite, cyberGlitchRGB 6s steps(1) infinite"},children:["MSMC",e.jsx("span",{"aria-hidden":!0,style:{position:"absolute",inset:0,color:"#f87171",opacity:.65,animation:"cyberGlitch 4s steps(1) infinite",clipPath:"inset(0 0 0 0)"},children:"MSMC"}),e.jsx("span",{"aria-hidden":!0,style:{position:"absolute",inset:0,color:"#22d3ee",opacity:.55,animation:"cyberGlitch 4s steps(1) infinite 0.2s",clipPath:"inset(0 0 0 0)"},children:"MSMC"})]})}),e.jsxs("div",{className:"text-center mb-5",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.3s both"},children:[e.jsx("div",{style:{fontSize:12,color:"var(--md-body-light)",letterSpacing:"0.12em"},children:"MINECRAFT SERVER MANAGEMENT CONSOLE"}),e.jsxs("div",{style:{fontSize:10,color:"var(--md-body-lighter)",marginTop:6,fontFamily:'Consolas, "JetBrains Mono", monospace',display:"inline-flex",alignItems:"center",gap:8},children:[e.jsx("span",{children:L}),e.jsx("span",{style:{opacity:.4},children:"·"}),e.jsx("span",{style:{padding:"1px 6px",borderRadius:2,fontSize:9,fontWeight:700,letterSpacing:"0.1em",color:m?"#f87171":f?"#34d399":n,border:`1px solid ${m?"#f8717160":f?"#34d39960":`${n}60`}`,background:m?"rgba(248,113,113,0.1)":f?"rgba(52,211,153,0.1)":`${n}10`},children:m?"⚠ SYSTEM ERROR":f?"✓ READY":"▶ BOOTING"})]})]}),e.jsxs("div",{className:"mb-4 flex items-center gap-2",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.45s both"},children:[e.jsx("div",{style:{width:6,height:6,borderRadius:"50%",backgroundColor:g,boxShadow:`0 0 10px ${g}`,animation:!m&&!f?"cyberPulse 1s ease-in-out infinite":"none"}}),e.jsx("span",{style:{fontSize:11,color:"var(--md-body-light)",fontFamily:'Consolas, "JetBrains Mono", monospace',maxWidth:420,overflow:"hidden",whiteSpace:"nowrap",textOverflow:"ellipsis"},children:p}),!m&&!f&&e.jsx("span",{style:{fontSize:11,color:g,fontFamily:"Consolas, monospace",animation:"cyberCursor 1s steps(1) infinite"},children:"▋"})]}),e.jsxs("div",{className:"w-full max-w-[560px] flex flex-col overflow-hidden rounded-xl",style:{animation:v?"none":"cyberBoot 0.8s ease-out 0.6s both",height:200,background:"rgba(15, 23, 42, 0.6)",backdropFilter:"blur(14px)",WebkitBackdropFilter:"blur(14px)",border:`1px solid ${n}30`,boxShadow:`0 4px 24px rgba(0,0,0,0.5), 0 0 60px ${n}10, inset 0 1px 0 rgba(255,255,255,0.06)`},children:[e.jsxs("div",{className:"flex items-center justify-between px-3 py-1.5 flex-shrink-0",style:{borderBottom:`1px solid ${n}20`,background:"rgba(2, 6, 23, 0.4)"},children:[e.jsxs("div",{className:"flex items-center gap-2",children:[e.jsxs("div",{style:{display:"flex",gap:5},children:[e.jsx("div",{style:{width:9,height:9,borderRadius:"50%",background:"#f87171",boxShadow:"0 0 4px rgba(248,113,113,0.5)"}}),e.jsx("div",{style:{width:9,height:9,borderRadius:"50%",background:"#fbbf24",boxShadow:"0 0 4px rgba(251,191,36,0.5)"}}),e.jsx("div",{style:{width:9,height:9,borderRadius:"50%",background:"#34d399",boxShadow:"0 0 4px rgba(52,211,153,0.5)"}})]}),e.jsx("div",{style:{width:8}}),e.jsx(I,{kind:"TerminalSolid",size:12,style:{color:n}}),e.jsx("span",{style:{fontSize:10,fontWeight:700,color:"var(--md-body-light)",letterSpacing:"0.1em"},children:"SYSTEM CONSOLE / tty/msmc0"})]}),e.jsxs("div",{className:"flex items-center gap-3",children:[e.jsxs("div",{style:{display:"flex",gap:8,fontFamily:"Consolas, monospace",fontSize:9},children:[e.jsxs("span",{style:{color:"#f87171"},children:["E:",ee]}),e.jsxs("span",{style:{color:"#fb923c"},children:["W:",re]}),e.jsxs("span",{style:{color:"#34d399"},children:["S:",te]}),e.jsxs("span",{style:{color:"var(--md-body-lighter)"},children:["N:",b.length]})]}),e.jsxs("span",{style:{fontSize:9,color:"var(--md-body-lighter)",fontFamily:"Consolas, monospace"},children:[b.length," entries"]}),e.jsx("div",{style:{width:6,height:6,borderRadius:"50%",backgroundColor:g,boxShadow:`0 0 8px ${g}`,animation:!m&&!f?"cyberPulse 1.5s ease-in-out infinite":"none"}})]})]}),e.jsxs("div",{ref:w,className:"flex-1 overflow-y-auto min-h-0",style:{padding:"8px 12px",scrollbarWidth:"thin"},children:[b.length===0&&e.jsxs("div",{style:{fontSize:11,color:"var(--md-body-lighter)",opacity:.4,textAlign:"center",padding:"20px 0",fontFamily:'Consolas, "JetBrains Mono", monospace'},children:["awaiting system signals",e.jsx("span",{style:{animation:"cyberCursor 1s steps(1) infinite"},children:"▋"})]}),b.map(t=>{const s=ce[t.tag]||"var(--md-body)",l=t.type==="error",d=t.type==="success",c=t.type==="warn",x=t.type==="info"||t.type==="debug"||t.type==="trace",h=t.message.match(/^(\s*\[[A-Z0-9_]+\])(.*)$/s),r=h?h[1]:"",u=h?h[2]:t.message,E=l?"✗":d?"✓":c?"!":x?"›":"·";return e.jsxs("div",{style:{display:"flex",gap:6,alignItems:"flex-start",fontFamily:'Consolas, "JetBrains Mono", "Cascadia Code", monospace',fontSize:11,lineHeight:1.7,marginBottom:1,padding:"1px 4px",borderRadius:2,animation:"cyberLogEntry 0.25s ease-out",backgroundColor:l?"rgba(239, 68, 68, 0.10)":d?"rgba(52, 211, 153, 0.07)":c?"rgba(251, 146, 60, 0.07)":"transparent",color:l?"#f87171":d?"#34d399":c?"#fb923c":"var(--md-body)",wordBreak:"break-word",whiteSpace:"pre-wrap",borderLeft:r?`2px solid ${s}`:"none",paddingLeft:r?6:4},children:[e.jsx("span",{style:{flexShrink:0,fontSize:9,color:"var(--md-body-lighter)",opacity:.5,userSelect:"none"},children:q(t.timestamp)}),e.jsx("span",{style:{flexShrink:0,color:l?"#f87171":d?"#34d399":c?"#fb923c":s,fontWeight:700,fontSize:11,userSelect:"none",width:10,textAlign:"center"},children:E}),r&&e.jsx("span",{style:{flexShrink:0,color:s,fontWeight:700,fontSize:10,userSelect:"none",animation:"cyberTagPop 0.3s ease-out"},children:r}),e.jsx("span",{style:{flex:1},children:u})]},t.id)})]}),e.jsxs("div",{className:"flex-shrink-0 px-3 py-1",style:{borderTop:`1px solid ${n}15`,background:"rgba(2, 6, 23, 0.5)",fontFamily:'Consolas, "JetBrains Mono", monospace',fontSize:9,color:"var(--md-body-lighter)",display:"flex",justifyContent:"space-between"},children:[e.jsx("span",{children:"root@msmc:~#"}),e.jsxs("span",{style:{opacity:.6},children:[j.current?"TAIL":"PAUSED"," · UTF-8 · LF"]})]})]}),e.jsx("div",{className:"mt-5",style:{height:40},children:m&&e.jsx("button",{onClick:Z,className:"px-6 py-2 text-white font-semibold rounded cursor-pointer border-none",style:{width:140,height:38,fontSize:12,letterSpacing:"0.15em",background:"linear-gradient(135deg, #dc2626, #991b1b)",boxShadow:"0 0 24px rgba(220,38,38,0.5), inset 0 1px 0 rgba(255,255,255,0.15)",transition:"all 150ms ease"},onMouseEnter:t=>{t.currentTarget.style.boxShadow="0 0 36px rgba(220,38,38,0.7), inset 0 1px 0 rgba(255,255,255,0.2)",t.currentTarget.style.transform="scale(1.04)"},onMouseLeave:t=>{t.currentTarget.style.boxShadow="0 0 24px rgba(220,38,38,0.5), inset 0 1px 0 rgba(255,255,255,0.15)",t.currentTarget.style.transform="scale(1)"},children:"⏻ SHUTDOWN"})})]}),e.jsxs("div",{className:"text-center pb-3 flex-shrink-0",style:{zIndex:10,pointerEvents:"none"},children:[e.jsx("div",{style:{fontSize:8,color:"var(--md-body-lighter)",opacity:.25,fontFamily:'Consolas, "JetBrains Mono", monospace',letterSpacing:"0.15em",whiteSpace:"pre",lineHeight:1.1,display:"none"},children:pe}),e.jsx("span",{style:{fontSize:9,color:"var(--md-body-lighter)",opacity:.35,fontFamily:'Consolas, "JetBrains Mono", monospace',letterSpacing:"0.2em"},children:"io.NET.ZTR_OS · SECURED · UTC+8 · © 2026 ABI-ZTROS"})]})]})]})}window.__msmcStartupScriptLoaded=!0;function C(o,a,p){try{const S=window.__msmc_bridge__;S&&typeof S.invoke=="function"&&S.invoke("log:write",{level:o,message:a,stack:p||"",url:location.href,ua:navigator.userAgent}).catch(()=>{})}catch{}}window.addEventListener("error",o=>{var p;const a=(o.message||"未知错误")+(o.filename?` @ ${o.filename}:${o.lineno||0}:${o.colno||0}`:"");console.error("[STARTUP-ERR]",a,o.error),C("Error",`[STARTUP-ERR] ${a}`,(p=o.error)==null?void 0:p.stack)});window.addEventListener("unhandledrejection",o=>{const a=o.reason,p=a&&(a.message||a.toString())||"未处理的 Promise 拒绝";console.error("[STARTUP-ERR] Unhandled rejection:",a),C("Error",`[STARTUP-ERR] 未处理的 Promise 拒绝: ${p}`,a==null?void 0:a.stack)});const B=document.getElementById("root");if(B)try{ne.createRoot(B).render(e.jsx(fe,{})),window.__msmcStartupReactMounted=!0,requestAnimationFrame(()=>{requestAnimationFrame(()=>{const o=document.getElementById("boot-diagnostics");if(!o||!o.parentNode)return;o.classList.add("fade-out");let a=!1;const p=()=>{a||(a=!0,o.parentNode&&o.parentNode.removeChild(o))};o.addEventListener("transitionend",p,{once:!0}),setTimeout(p,600)})})}catch(o){const a=o instanceof Error?o.stack:String(o);C("Error",`[STARTUP-ERR] React 渲染异常: ${String(o)}`,a);const p=document.getElementById("boot-log");p&&(p.textContent+=`[FATAL] React 渲染失败: ${String(o)}
${a||""}
`)}
