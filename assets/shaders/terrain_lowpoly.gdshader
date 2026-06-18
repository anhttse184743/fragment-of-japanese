shader_type spatial;
render_mode cull_back, diffuse_lambert_wrap;

// Texture biome — gán từ ShaderMaterial trong TerrainNode
uniform sampler2D tex_grass : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_dirt  : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_rock  : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_sand  : hint_default_white, filter_linear_mipmap, repeat_enable;

// Tiling: số nhỏ = tile to, số lớn = tile nhỏ
uniform float tex_scale : hint_range(0.05, 2.0) = 0.5;

void fragment() {
    vec2 uv = UV * tex_scale;

    vec3 c_grass = texture(tex_grass, uv).rgb;
    vec3 c_dirt  = texture(tex_dirt,  uv).rgb;
    vec3 c_rock  = texture(tex_rock,  uv).rgb;
    vec3 c_sand  = texture(tex_sand,  uv).rgb;

    // Vertex COLOR lưu blend weight:
    //   r = grass weight
    //   g = dirt  weight
    //   b = rock  weight
    //   a = sand  weight
    // Tổng r+g+b+a = 1.0 (tính từ TerrainMeshBuilder)
    vec3 col = c_grass * COLOR.r
             + c_dirt  * COLOR.g
             + c_rock  * COLOR.b
             + c_sand  * COLOR.a;

    ALBEDO    = col;
    ROUGHNESS = 0.85;
    METALLIC  = 0.0;
}
