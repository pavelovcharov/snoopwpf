sampler2D implicitInputSampler : register(S0);
float2 pixelSize : register(C0);
float4 selectedColor : register(C1);

float4 main(float2 uv : TEXCOORD) : COLOR{
    float4 current = tex2D(implicitInputSampler, uv);
    if(current.r==current.g || current.r==1&& current.g==0 && current.b==1)
        return float4(0,0,0,0);           
    float2 border = 3*pixelSize;            
    
    float4 neighbour = tex2D(implicitInputSampler, uv-float2(border.x,0));
    if(any(current-neighbour))
        return float4(0.16,0.48,0.85,1);                     
    
    neighbour = tex2D(implicitInputSampler, uv+float2(border.x,0));
    if(any(current-neighbour))
        return float4(0.16,0.48,0.85,1);
    
    neighbour = tex2D(implicitInputSampler, uv-float2(0, border.y));
        if(any(current-neighbour))
            return float4(0.16,0.48,0.85,1);
            
    neighbour = tex2D(implicitInputSampler, uv+float2(0,border.y));
         if(any(current-neighbour))
             return float4(0.16,0.48,0.85,1);                    
    
    if(selectedColor.a!=0){
        if(!any(current-selectedColor)){
            float2 pSize = uv/pixelSize;
            float pNX = fmod(pSize.x+pSize.y, 15);
            if(pNX<1.2) 
                return float4(0.16,0.48,0.85,1);          
//            if(!any(pN%10))        
        }        
    }
    
    return float4(0,0,0,0);              	
}