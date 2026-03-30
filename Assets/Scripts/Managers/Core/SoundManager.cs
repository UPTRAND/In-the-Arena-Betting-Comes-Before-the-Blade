using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.Diagnostics;
using System.Collections.Generic;
/// <summary>
/// 사운드 관리
/// BGM, SFX 키고 끌수 있으며 상황에 따라 사운드 크기가 서서히 바뀌던지 BGM을 무작위로 루프할 수 있음
/// </summary>
public class SoundManager : MonoBehaviour
{
    public AudioMixer Mixer { get; private set; }

    [Header("BGM")]
    List<AudioClip> bgmClips = new List<AudioClip>();
    float bgmVolume = 0.5f;
    AudioSource bgmPlayer;
    AudioSource bgmBuffer;
    bool isBGMLooping = false;

    [Header("SFX")]
    List<AudioClip> sfxClips = new List<AudioClip>();
    float sfxVolume = 0.5f;
    int channels = 20;
    AudioSource[] sfxPlayers;
    int channelIndex;

    // AudioMixer - Master 볼륨
    public float MasterSoundVolume
    {
        get
        {
            float temp;
            Mixer.GetFloat("MasterSound", out temp);
            return Mathf.Pow(10, temp * 0.05f);
        }

        set
        {
            Mixer.SetFloat("MasterSound", Mathf.Log10(value) * 20);
        }
    }
    // AudioMixer - BGM 볼륨
    public float BGMSoundVolume
    {
        get
        {
            float temp;
            Mixer.GetFloat("BGMSound", out temp);
            return Mathf.Pow(10, temp * 0.05f);
        }

        set
        {
            Mixer.SetFloat("BGMSound", Mathf.Log10(value) * 20);
        }
    }
    // AudioMixer - SFX 볼륨
    public float SFXSoundVolume
    {
        get
        {
            float temp;
            Mixer.GetFloat("SFXSound", out temp);
            return Mathf.Pow(10, temp * 0.05f);
        }

        set
        {
            Mixer.SetFloat("SFXSound", Mathf.Log10(value) * 20);
        }
    }

    // 초기화 BGM은 메인과 버퍼 2개가 있으며 SFX는 채널수를 지정해서 그 갯수만큼 만듦
    public void Init()
    {
        Mixer = Managers.Resource.Load<AudioMixer>($"Sound/Mixer");

        GameObject bgmObject = new GameObject("BgmPlayer");

        bgmObject.transform.parent = transform;
        bgmPlayer = bgmObject.AddComponent<AudioSource>();
        bgmPlayer.outputAudioMixerGroup = Mixer.FindMatchingGroups("BGM")[0];
        bgmPlayer.playOnAwake = false;
        bgmPlayer.loop = true;
        bgmPlayer.volume = bgmVolume;
        bgmPlayer.dopplerLevel = 0.0f;
        bgmPlayer.reverbZoneMix = 0.0f;

        GameObject bufferObject = new GameObject("BgmPlayer");
        bufferObject.transform.parent = transform;
        bgmBuffer = bufferObject.AddComponent<AudioSource>();
        bgmBuffer.outputAudioMixerGroup = Mixer.FindMatchingGroups("BGM")[0];
        bgmBuffer.playOnAwake = false;
        bgmBuffer.loop = true;
        bgmBuffer.volume = bgmVolume;
        bgmBuffer.dopplerLevel = 0.0f;
        bgmBuffer.reverbZoneMix = 0.0f;

        GameObject sfxObject = new GameObject("SfxPlayer");
        sfxObject.transform.parent = transform;

        sfxPlayers = new AudioSource[channels];

        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            sfxPlayers[index] = sfxObject.AddComponent<AudioSource>();
            sfxPlayers[index].outputAudioMixerGroup = Mixer.FindMatchingGroups("SFX")[0];
            sfxPlayers[index].playOnAwake = false;
            sfxPlayers[index].volume = sfxVolume;
            sfxPlayers[index].dopplerLevel = 0.0f;
            sfxPlayers[index].reverbZoneMix = 0.0f;
        }

        isBGMLooping = false;

        AddBGMClips();
        AddSFXClips();
    }

    void AddBGMClips()
    {
        for (int i = 0; i < (int)Define.BGM.Count; i++)
        {
            bgmClips.Add(Managers.Resource.Load<AudioClip>($"Sound/BGM/{(Define.BGM)i}"));
        }
    }

    void AddSFXClips()
    {
        for (int i = 0; i < (int)Define.SFX.Count; i++)
        {
            sfxClips.Add(Managers.Resource.Load<AudioClip>($"Sound/SFX/{(Define.SFX)i}"));
        }
    }

    // BGM을 실행
    public void PlayBgm(Define.BGM bgm, bool isLoop)
    {
        if (bgmPlayer.isPlaying)
        {
            bgmBuffer.clip = bgmClips[(int)bgm];
            bgmBuffer.Play();
            bgmBuffer.volume = bgmVolume;
            StartCoroutine(SoundSmooth(bgmPlayer, true));
            StartCoroutine(SoundSmooth(bgmBuffer, false));

            var temp = bgmBuffer;
            bgmBuffer = bgmPlayer;
            bgmPlayer = temp;

            bgmPlayer.loop = isLoop;
            bgmBuffer.loop = isLoop;
        }
        else
        {
            bgmPlayer.clip = bgmClips[(int)bgm];
            bgmPlayer.volume = bgmVolume;
            bgmPlayer.Play();
            StartCoroutine(SoundSmooth(bgmPlayer, false));
            bgmPlayer.loop = isLoop;
        }
    }
    // BGM을 멈춤
    public void StopBgm()
    {
        StartCoroutine(SoundSmooth(bgmPlayer, true));
        StopCoroutine("BGMRandomLoop");
        isBGMLooping = false;
    }

    // SFX를 실행
    public void PlaySFX(Define.SFX sfx, float Pitch = 1, bool isLoop = false)
    {
        if (Pitch < 0)
            Pitch = Random.Range(0.8f, 1.2f);

        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            int loopIndex = (index + channelIndex) % sfxPlayers.Length;

            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[(int)sfx];
            sfxPlayers[loopIndex].loop = isLoop;
            sfxPlayers[loopIndex].pitch = Pitch;
            sfxPlayers[loopIndex].volume = sfxVolume;
            sfxPlayers[loopIndex].Play();
            break;
        }
    }
    // SFX를 서서히 실행
    public void SmoothPlaySfx(Define.SFX sfx, float Pitch = 1, bool isLoop = false)
    {
        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            int loopIndex = (index + channelIndex) % sfxPlayers.Length;

            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[(int)sfx];
            sfxPlayers[loopIndex].loop = isLoop;
            sfxPlayers[loopIndex].pitch = Pitch;
            sfxPlayers[loopIndex].Play();
            StartCoroutine(SoundSmooth(sfxPlayers[loopIndex], false));
            break;
        }
    }
    // SFX를 멈춤
    public void StopSfx(Define.SFX sfx)
    {
        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            if (sfxPlayers[index].clip == sfxClips[(int)sfx])
            {
                StartCoroutine(SoundSmooth(sfxPlayers[index], true));
            }
        }
    }

    // 특정 값까지 BGM을 서서히 줄임
    public void SetBGMSoundVolume(float val)
    {
        StartCoroutine(BGMSmoothVolum(val, 1));
    }

    // BGM을 실행하고 끝났을시 랜덤으로 다시 돌림
    public void StartBGMRandomLoop(int num)
    {
        if (isBGMLooping) return;
        StopCoroutine("BGMRandomLoop");
        StartCoroutine("BGMRandomLoop", num);
        isBGMLooping = true;
    }

    // 사운드 크기가 특정값까지 자연스럽게 바뀜
    IEnumerator BGMSmoothVolum(float endVolum, float time)
    {
        float DeltaVolum = (endVolum - bgmPlayer.volume) * 0.1f;
        float second = time * 0.1f;

        for (int i = 0; i < 10; i++)
        {
            bgmPlayer.volume += DeltaVolum;
            yield return new WaitForSeconds(second);
        }
    }

    // 사운드의 크기를 서서히 줄이거나 늘릴때 사용 
    IEnumerator SoundSmooth(AudioSource audio, bool isDown)
    {
        float startvolume = audio.volume;
        float start = 0;
        float num = 0;
        if (isDown)
        {
            start = audio.volume;
            num = -audio.volume / 10;
        }
        else
        {
            start = 0;
            num = audio.volume / 10;
        }

        audio.volume = start;

        for (int i = 0; i < 10; i++)
        {
            audio.volume += num;
            yield return new WaitForSeconds(0.095f);
        }

        if (isDown)
        {
            audio.Stop();
            audio.volume = startvolume;
        }
    }

    // 2초마다 한번씩 BGM이 끝났는지 검사 후 끝났으면 다음 BGM을 틀음
    IEnumerator BGMRandomLoop(int num)
    {
        int[] temp = new int[num];
        for (int i = 0; i < num; i++)
            temp[i] = i;

        Util.SuffleArray(temp);

        int index = 0;

        while (true)
        {
            if (!bgmPlayer.isPlaying)
            {
                PlayBgm((Define.BGM)temp[index], false);
                index++;

                if (index >= temp.Length)
                {
                    index = 0;
                    Util.SuffleArray(temp);
                }
            }
            yield return new WaitForSeconds(1);
        }
    }

    public void Clear()
    {
    }
}