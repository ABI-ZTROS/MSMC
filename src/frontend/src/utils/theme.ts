import type { SettingsData } from '@/types/bridge'

export function argbToRgb(hex: string): string {
  if (!hex || hex.length < 7) return hex
  if (hex.length === 9 && hex.startsWith('#')) {
    return '#' + hex.slice(3)
  }
  return hex
}

function hexToRgb(hex: string): { r: number; g: number; b: number } {
  const clean = argbToRgb(hex).replace('#', '')
  const r = parseInt(clean.substring(0, 2), 16)
  const g = parseInt(clean.substring(2, 4), 16)
  const b = parseInt(clean.substring(4, 6), 16)
  return { r, g, b }
}

function rgbToHex(r: number, g: number, b: number): string {
  const toHex = (v: number) =>
    Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`
}

function lighten(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex)
  return rgbToHex(
    r + (255 - r) * amount,
    g + (255 - g) * amount,
    b + (255 - b) * amount,
  )
}

function darken(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex)
  return rgbToHex(r * (1 - amount), g * (1 - amount), b * (1 - amount))
}

function rgba(hex: string, alpha: number): string {
  const { r, g, b } = hexToRgb(hex)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

function applyPrimaryColor(primary: string): void {
  const style = document.documentElement.style
  const p = argbToRgb(primary)
  style.setProperty('--md-primary-hue-lighter', lighten(p, 0.5))
  style.setProperty('--md-primary-hue-light', lighten(p, 0.25))
  style.setProperty('--md-primary-hue-mid', p)
  style.setProperty('--md-primary-hue-dark', darken(p, 0.15))
  style.setProperty('--md-primary-hue-darker', darken(p, 0.25))
  style.setProperty('--md-nav-item-selected', p)
  style.setProperty('--md-nav-item-hover', rgba(p, 0.12))
  style.setProperty('--md-nav-item-selected-hover', darken(p, 0.1))
  style.setProperty('--md-nav-item-selected-indicator', p)
  style.setProperty('--md-primary-subtle-background', rgba(p, 0.1))
  style.setProperty('--md-primary-subtle-border', rgba(p, 0.2))
  style.setProperty('--md-accent-gradient-start', lighten(p, 0.25))
  style.setProperty('--md-accent-gradient-end', p)
  style.setProperty(
    '--md-accent-gradient',
    `linear-gradient(90deg, ${lighten(p, 0.25)} 0%, ${p} 100%)`,
  )
  style.setProperty('--md-success-foreground', lighten(p, 0.4))
}

export function applySettingsToCss(settings: SettingsData): void {
  const style = document.documentElement.style
  const primary = argbToRgb(settings.primaryColorHex)
  const accent = argbToRgb(settings.accentColorHex)
  const bg = argbToRgb(settings.backgroundColorHex)
  const card = argbToRgb(settings.cardColorHex)
  const text = argbToRgb(settings.textColorHex)
  const border = argbToRgb(settings.borderColorHex)

  style.setProperty('--md-paper', bg)
  style.setProperty('--md-deep-background', darken(bg, 0.5))
  style.setProperty('--md-card-background', card)
  style.setProperty('--md-card-hover', lighten(card, 0.08))
  style.setProperty('--md-terminal-background', darken(card, 0.15))
  style.setProperty('--md-loading-overlay', rgba(card, 0.8))

  applyPrimaryColor(primary)

  style.setProperty('--md-accent-text', accent)
  style.setProperty('--md-accent-subtle-border', rgba(accent, 0.2))

  style.setProperty('--md-body', text)
  style.setProperty('--md-body-light', darken(text, 0.3))
  style.setProperty('--md-body-lighter', darken(text, 0.45))

  style.setProperty('--md-subtle-border', border)
  style.setProperty('--md-card-subtle-border', rgba(border, 0.2))

  style.setProperty('--md-info-foreground', darken(text, 0.3))

  style.setProperty('--md-radius', `${settings.cornerRadius}px`)
  style.setProperty('--md-radius-small', `${Math.max(4, settings.cornerRadius - 4)}px`)
  style.setProperty('--md-radius-large', `${settings.cornerRadius + 4}px`)
  style.setProperty('--md-duration-normal', `${settings.animationDuration}ms`)
  style.setProperty('--md-enable-animations', settings.enableAnimations ? '1' : '0')
}

export { applyPrimaryColor }
