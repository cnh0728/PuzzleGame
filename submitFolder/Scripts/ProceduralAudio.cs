using UnityEngine;

public static class ProceduralAudio
{
    // C# 코드로 짧은 톤의 효과음(AudioClip)을 즉석 생성
    public static AudioClip CreateToneClip(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 사인파 계산 + 스르륵 줄어드는 페이드 아웃
            float fade = 1f - (t / duration);
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * fade;
        }

        AudioClip clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}