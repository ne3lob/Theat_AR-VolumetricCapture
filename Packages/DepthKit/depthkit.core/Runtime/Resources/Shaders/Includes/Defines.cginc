#ifndef _DEPTHKIT_DEFINES_CGINC
#define _DEPTHKIT_DEFINES_CGINC

#define DK_BRIGHTNESS_THRESHOLD_OFFSET 0.01f
#define DK_MAX_NUM_PERSPECTIVES 10

#define DK_CORRECT_NONE 0
#define DK_CORRECT_LINEAR_TO_GAMMA 1
#define DK_CORRECT_GAMMA_TO_LINEAR 2
//Unity 2017.1 - 2018.2 has a video player bug where Linear->Gamma needs to be applied twice before texture look up in depth
#define DK_CORRECT_LINEAR_TO_GAMMA_2X 3

#endif