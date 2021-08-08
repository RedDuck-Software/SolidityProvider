namespace AbiTypeProvider

// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("AbiTypeProviderFromTruffle.DesignTime.dll")>]
do ()
