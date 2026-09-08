/**
 * 剩余的 CSS keyframes 字符串
 * 只有 3 个：breathe（呼吸光晕）+ successPop（完成态对勾弹出）+ fadeOut（页面淡出）
 */
export const STARTUP_KEYFRAMES = `
@keyframes startupBreathe {
  0%, 100% { opacity: 0.85; }
  50%      { opacity: 1; }
}

@keyframes startupSuccessPop {
  0%   { transform: scale(0); opacity: 0; }
  60%  { transform: scale(1.2); opacity: 1; }
  100% { transform: scale(1); opacity: 1; }
}

@keyframes startupFadeOut {
  0%   { opacity: 1; }
  100% { opacity: 0; }
}
`;
