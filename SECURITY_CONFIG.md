# OPC UA Security Configuration

## Overview

The `OpcUaTagReader` now includes a robust security configuration with PKI (Public Key Infrastructure) support for OPC UA certificate-based authentication.

## PKI Directory Structure

When you run the application, it will automatically create the following directory structure in the application's base directory:

```
pki/
??? own/          # Your client application certificate
??? trusted/      # Trusted server certificates
??? issuers/      # Trusted certificate issuers for chain validation
??? rejected/     # Rejected/revoked certificates
```

These directories are created automatically on first run.

## Configuration

The security configuration is defined in `BuildOpcConfigurationAsync()` method in `OpcUaTagReader.cs`:

### Key Components:

1. **Application Certificate** (`pki/own`)
   - Subject: `CN=IgnitionHeartbeatMonitor`
   - Used to identify your client to the OPC UA server
   - Generated automatically if missing

2. **Trusted Peer Certificates** (`pki/trusted`)
   - Contains certificates of servers you trust
   - Place your OPC UA server's certificate here if using secure connections

3. **Trusted Issuer Certificates** (`pki/issuers`)
   - Used for certificate chain validation
   - Important for validating server certificates signed by CAs

4. **Rejected Certificate Store** (`pki/rejected`)
   - Tracks certificates that have been explicitly rejected
   - Prevents accidental re-acceptance of problematic certificates

## Usage Notes

### For Lab/Development:
```csharp
AutoAcceptUntrustedCertificates = true   // Fine for lab/testing
```

The current configuration accepts untrusted certificates, which is suitable for:
- Development environments
- Lab setups
- Internal testing with self-signed certificates

### For Production:
1. Generate proper certificates for your application
2. Export your OPC UA server's certificate and place it in `pki/trusted/`
3. Set `AutoAcceptUntrustedCertificates = false`
4. Place issuer certificates in `pki/issuers/` for chain validation

## Troubleshooting

### Certificate Errors

If you encounter certificate validation errors:

1. **Server certificate not accepted**: Place the server's certificate in `pki/trusted/`
2. **Certificate chain validation failed**: Add issuer certificates to `pki/issuers/`
3. **Application certificate missing**: Check that `pki/own/` exists and has proper permissions

### Viewing Certificates

To inspect certificates in the PKI directories, you can use:
- **Windows**: Certificate Manager (certmgr.msc)
- **OpenSSL**: `openssl x509 -in certificate.cer -text -noout`

## Diagnostic Output

The application logs PKI initialization steps:

```
[OpcUaTagReader] PKI root directory: C:\...\pki
[OpcUaTagReader] Creating PKI directories...
[OpcUaTagReader] ? PKI directories created/verified
[OpcUaTagReader] ? Application configuration validated
```

## Migration from Old Configuration

If you were using the simpler configuration without PKI:

1. The old configuration with `SecurityConfiguration` still works
2. The new `BuildOpcConfigurationAsync()` provides better security practices
3. No changes needed to `Program.cs` - all handled in `OpcUaTagReader`

## References

- [OPC UA Security Best Practices](https://opcfoundation.org/developer-tools/specifications-unified-architecture/part-2-security-model/)
- [OPC.Net UA Library Documentation](https://github.com/OPCFoundation/Opc.Ua-.NETStandard)
