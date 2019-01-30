sampler2D implicitInputSampler : register(S0);
float4 visibleRect : register(C0);

float4 main(float2 uv : TEXCOORD) : COLOR{	   
	float4 color = tex2D(implicitInputSampler, uv);

//	if(uv.x<0 || uv.x>0.1 || uv.y<0 || uv.y>0.1){	
	if(uv.x<visibleRect.x || uv.x>visibleRect.z || uv.y<visibleRect.y || uv.y>visibleRect.w){
	    color.rgb = color.r*0.299+color.g*0.578+color.b*0.114;    	     	
	}                        	
	return color;
}