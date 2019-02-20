sampler2D implicitInputSampler : register(S0);
float2 pixelSize : register(C0);
float4 selectedColor : register(C1);

float4 main(float2 uv : TEXCOORD) : COLOR{
    float4 current = tex2D(implicitInputSampler, uv);
//    if(current.r==current.g || current.r==1&& current.g==0 && current.b==1)
    if(current.r==current.g || !any(current-float4(1,0,1,1)))
        return float4(0,0,0,0);           
    float2 border = 2*pixelSize;            
    float2 bx = float2(border.x,0);
    float2 by = float2(0, border.y);
    
    if(any(current-tex2D(implicitInputSampler, uv-bx)))
        return float4(0.16,0.48,0.85,1);                     

    if(any(current-tex2D(implicitInputSampler, uv+bx)))
        return float4(0.16,0.48,0.85,1);

    if(any(current-tex2D(implicitInputSampler, uv-by)))
        return float4(0.16,0.48,0.85,1);

    if(any(current-tex2D(implicitInputSampler, uv+by)))
        return float4(0.16,0.48,0.85,1);
             
    if(any(current-tex2D(implicitInputSampler, uv-border)))
        return float4(0.16,0.48,0.85,1);                     

    if(any(current-tex2D(implicitInputSampler, uv+border)))
        return float4(0.16,0.48,0.85,1);                                           
    
    if(!any(current-selectedColor)){
        float2 pSize = uv/pixelSize;
        float pNX = fmod(pSize.x+pSize.y, 15);
        if(pNX<1.2) 
            return float4(0.16,0.48,0.85,1);                 
    }        
    
    return float4(0,0,0,0);              	
}