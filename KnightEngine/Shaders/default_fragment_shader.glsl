#version 330 core

in vec2 TexCoord;
in vec3 Color;

out vec4 FragColor;

uniform sampler2D texture0;

void main()
{
    vec4 texColor = texture(texture0, TexCoord);
    FragColor = texColor * vec4(Color, 1.0);
}