import logoImg from '../../assets/logo/final/msmc-logo-256.jpg';

/**
 * MSMC 二次元少女 Logo
 * 使用 256px JPG (21KB) — 正好适配启动页 200px 显示尺寸，比 2MB 的 square.png 加载快 100 倍
 */
export function StartupLogo() {
  return (
    <img
      src={logoImg}
      alt="MSMC Logo"
      style={{
        width: 200,
        height: 200,
        objectFit: 'contain',
        userSelect: 'none',
        // 微呼吸动效（跟背景呼吸同节奏，6s）
        animation: 'startupBreathe 6s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        pointerEvents: 'none',
      }}
      draggable={false}
    />
  );
}
