Add-Type -AssemblyName System.Speech
$base = "C:\Users\rafae\Work\TLT\spikes\WhisperSpike\audio"
$texto = Get-Content -Raw (Join-Path $base "referencia.txt")
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$s.SelectVoice("Microsoft Zira Desktop")
$s.Rate = 0
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
$s.SetOutputToWaveFile((Join-Path $base "fala-en.wav"), $fmt)
$s.Speak($texto)
$s.Dispose()
Write-Output "gerado com sucesso"
