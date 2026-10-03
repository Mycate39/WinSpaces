# Diagnostic WinSpaces : lance l'application, observe si elle survit et
# rassemble journal + erreurs Windows dans un fichier texte sur le Bureau.
$ErrorActionPreference = 'Continue'
$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe     = Join-Path $here 'WinSpaces.exe'
$desktop = [Environment]::GetFolderPath('Desktop')
$out     = Join-Path $desktop 'WinSpaces-diagnostic.txt'
$logDir  = Join-Path $env:LOCALAPPDATA 'WinSpaces'
$log     = Join-Path $logDir 'winspaces.log'
$report  = New-Object System.Collections.Generic.List[string]
$since   = Get-Date
function Note([string]$text) { $report.Add($text); Write-Host $text }

function Describe-ExitCode([int]$code) {
    switch ('0x{0:X8}' -f $code) {
        '0xE0434352' { 'exception .NET non gérée' }
        '0xC0000005' { 'violation d''accès mémoire (crash natif)' }
        '0xC0000409' { 'arrêt immédiat (FailFast / dépassement de pile)' }
        '0xC00000FD' { 'dépassement de pile' }
        '0x00000000' { 'arrêt normal' }
        default      { 'code inconnu' }
    }
}

function Test-Launch([string]$label, [string]$arguments) {
    Note ""
    Note "=== Lancement $label ==="
    $start = Get-Date
    try {
        if ($arguments) { $p = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru }
        else            { $p = Start-Process -FilePath $exe -PassThru }
    } catch {
        Note "Impossible de lancer l'exécutable : $($_.Exception.Message)"
        return $false
    }
    Write-Host "Attente de 15 secondes..."
    Start-Sleep -Seconds 15
    $p.Refresh()
    if ($p.HasExited) {
        $code = $p.ExitCode
        Note ("Le processus s'est ARRÊTÉ. Code de sortie : 0x{0:X8} ({1})" -f $code, (Describe-ExitCode $code))
        return $false
    }
    $mem = [math]::Round($p.WorkingSet64 / 1MB)
    Note "Le processus TOURNE toujours (pid $($p.Id), $mem Mo, répond : $($p.Responding))."
    return $true
}

Note "===== Diagnostic WinSpaces — $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ====="
try {
    $os = Get-CimInstance Win32_OperatingSystem
    $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    Note "Windows : $($os.Caption) — $($cv.DisplayVersion) build $($cv.CurrentBuild).$($cv.UBR) ($($os.OSArchitecture))"
} catch { Note "Version Windows illisible : $($_.Exception.Message)" }

if (-not (Test-Path $exe)) {
    Note "ERREUR : WinSpaces.exe introuvable à côté du script ($exe)."
} else {
    $item = Get-Item $exe
    Note "Exécutable : $exe — $([math]::Round($item.Length / 1MB)) Mo — version $($item.VersionInfo.ProductVersion)"
    if (Get-Item $exe -Stream Zone.Identifier -ErrorAction SilentlyContinue) {
        Note "Le fichier porte la marque « téléchargé depuis Internet » : déblocage."
        Unblock-File $exe
    }

    $running = Get-Process -Name WinSpaces -ErrorAction SilentlyContinue
    if ($running) {
        Note "Instance(s) déjà en cours (pid $($running.Id -join ', ')) : arrêt avant le test."
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }

    $startTime = Get-Date
    $ok = Test-Launch 'normal' ''
    if (-not $ok) {
        $ok = Test-Launch 'en mode sans échec (--safe)' '--safe'
        if ($ok) { Note "=> Le mode sans échec fonctionne : le problème vient des gestes, du plein écran automatique ou de la barre de menu." }
    } else {
        Note "=> WinSpaces fonctionne : cherchez son icône dans la zone « ^ » à droite de la barre des tâches."
        Write-Host ""
        Write-Host "================ TEST INTERACTIF ================" -ForegroundColor Cyan
        Write-Host "Faites ces manipulations, dans l'ordre, puis revenez ici :"
        Write-Host "  1. Ctrl+Alt+N            (crée un nouvel espace)"
        Write-Host "  2. Ctrl+Alt+Flèche gauche, puis Ctrl+Alt+Flèche droite"
        Write-Host "  3. Balayage 3 doigts vers la gauche, puis vers la droite"
        Write-Host "  4. Balayage 4 doigts vers la gauche, puis vers la droite"
        Write-Host "(L'espace créé à l'étape 1 peut être supprimé ensuite avec Win+Tab.)"
        Read-Host "Appuyez sur Entrée quand c'est fait" | Out-Null
        Note "Test interactif effectué (Ctrl+Alt+N, Ctrl+Alt+flèches, balayages 3 et 4 doigts)."
    }
}

Note ""
Note "=== Journal ($log) — depuis le dernier lancement ==="
if (Test-Path $log) {
    $lines = @(Get-Content $log -Encoding UTF8)
    $startIdx = 0
    for ($i = $lines.Count - 1; $i -ge 0; $i--) { if ($lines[$i] -like '*===== Lancement*') { $startIdx = $i; break } }
    # Le dernier lancement est celui du mode sans échec s'il a eu lieu : on remonte
    # d'un lancement de plus pour inclure aussi le lancement normal.
    if ($startIdx -gt 0 -and -not $ok) {
        for ($i = $startIdx - 1; $i -ge 0; $i--) { if ($lines[$i] -like '*===== Lancement*') { $startIdx = $i; break } }
    }
    # Lignes répétées regroupées, 250 lignes au maximum.
    $previous = $null; $repeat = 0; $kept = 0
    foreach ($line in $lines[$startIdx..($lines.Count - 1)]) {
        $body = $line -replace '^\[[^\]]*\] ', ''
        if ($body -eq $previous) { $repeat++; continue }
        if ($repeat -gt 0) { $report.Add("    (… ligne précédente répétée $repeat fois)"); $repeat = 0 }
        $previous = $body
        $report.Add($line); $kept++
        if ($kept -ge 250) { $report.Add("    (… journal tronqué)"); break }
    }
    if ($repeat -gt 0) { $report.Add("    (… ligne précédente répétée $repeat fois)") }
}
else { Note "Aucun journal : WinSpaces n'a jamais atteint son point d'entrée sur cette machine." }

Note ""
Note "=== Erreurs Windows liées à WinSpaces (pendant ce test) ==="
try {
    $events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $since } -ErrorAction Stop |
        Where-Object { $_.Message -match 'WinSpaces' } | Select-Object -First 8
    if ($events) {
        foreach ($e in $events) {
            $report.Add("--- $($e.TimeCreated) | $($e.ProviderName) | id $($e.Id)")
            $report.Add($e.Message)
        }
    } else { Note "Aucune erreur Windows pendant le test." }
} catch { Note "Lecture de l'Observateur d'événements impossible : $($_.Exception.Message)" }

$report | Out-File -FilePath $out -Encoding UTF8
Write-Host ""
Write-Host "Rapport enregistré : $out"
Start-Process notepad.exe $out
