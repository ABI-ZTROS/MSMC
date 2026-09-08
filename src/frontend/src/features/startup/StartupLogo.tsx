import logoImg from '../../assets/logo/final/msmc-logo-square.png';

/**
 * MSMC 二次元少女 Logo
 * v8: 微侧 45° 朝右、冰蓝色眼睛、三态渐变头发、银色圆框眼镜、黑色头戴式耳机、闭唇微笑
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
