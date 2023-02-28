using UnityEngine;

public class SoundChanger : MonoBehaviour
{
    public AudioClip firstSound;
    public AudioClip secondSound;
    public float timeToChange ; // Time to change sound in minutes

    private float timeElapsed = 0f;
    private bool hasChanged = false;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = firstSound;
        audioSource.Play();
    }

    void Update()
    {
       // Debug.Log(timeElapsed);
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= timeToChange * 60f && !hasChanged) // Time to change sound has elapsed
        {
            audioSource.clip = secondSound;
            audioSource.Play();
            hasChanged = true; // Prevent changing sound again
        }
        
    }
}




