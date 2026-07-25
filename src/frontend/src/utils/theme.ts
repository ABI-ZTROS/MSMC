import type { SettingsData } from '@/types/bridge'

export function argbToRgb(hex: string): string {
  if (!hex || hex.length < 7) return hex
  if (hex.length === 9 && hex.startsWith('#')) {
    return '#' + hex.slice(3)
  }
  return hex
}

export function applySettingsToCss(settings: SettingsData): void {
  const style = document.documentElement.style
  style.setProperty('--md-primary-hue-mid', argbToRgb(settings.primaryColorHex))
  style.setProperty('--md-accent-text', argbToRgb(settings.accentColorHex))
  style.setProperty('--md-paper', argbToRgb(settings.backgroundColorHex))
  style.setProperty('--md-card-background', argbToRgb(settings.cardColorHex))
  style.setProperty('--md-body', argbToRgb(settings.textColorHex))
  style.setProperty('--md-subtle-border', argbToRgb(settings.borderColorHex))
  style.setProperty('--md-radius', `${settings.cornerRadius}px`)
  style.setProperty('--md-radius-small', `${Math.max(4, settings.cornerRadius - 4)}px`)
  style.setProperty('--md-radius-large', `${settings.cornerRadius + 4}px`)
  style.setProperty('--md-duration-normal', `${settings.animationDuration}ms`)
}
