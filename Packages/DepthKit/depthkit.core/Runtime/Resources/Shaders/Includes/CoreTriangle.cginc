#ifndef _DK_CORE_TRIANGLE_TYPES_CGINC
#define _DK_CORE_TRIANGLE_TYPES_CGINC

struct Triangle
{
#ifndef DK_CORE_PACKED_TRIANGLE
    uint perspectiveIndex;
    uint vertex[3];
#else 
    Vertex vertex[3];
#endif
};

Triangle newTriangle()
{
    Triangle t;
#ifndef DK_CORE_PACKED_TRIANGLE
    t.perspectiveIndex = 0;
    t.vertex[0] = 0;
    t.vertex[1] = 0;
    t.vertex[2] = 0;
#else 
    t.vertex[0] = newVertex();
    t.vertex[1] = newVertex();
    t.vertex[2] = newVertex();
#endif
    return t;
}

#endif