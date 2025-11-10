# Siemens S7 Communication Protocol Documentation

## Official Manuals

### 1. S7-1500 Communication Function Manual
- **URL**: https://support.industry.siemens.com/cs/attachments/59192925/s71500_communication_function_manual_en-US_en-US.pdf
- **Filename**: S7-1500_Communication_Function_Manual.pdf
- **Description**: Communication functions for S7-1500 series

### 2. Communication in S7 Stations
- **URL**: https://cache.industry.siemens.com/dl/files/198/30374198/att_63131/v1/MN_s7-cps-ie_76.pdf
- **Filename**: S7_Communication_MN_s7-cps-ie_76.pdf
- **Description**: S7 station communication overview

### 3. RFC 1006 - ISO Transport over TCP
- **URL**: https://www.rfc-editor.org/rfc/rfc1006.txt
- **Filename**: RFC1006_ISO_Transport_over_TCP.txt
- **Description**: ISO transport services on top of TCP (used by S7 protocol)

## Protocol Stack Information

The S7 protocol uses:
- **TPKT**: RFC 1006 (ISO transport over TCP), updated by RFC 2126
- **COTP**: ISO 8073 Connection-Oriented Transport Protocol (RFC 905)
- **TCP Port**: 102 (well-known port for TPKT traffic)

## Additional Resources

### Community Documentation
- **Snap7**: https://snap7.sourceforge.net/siemens_comm.html
- **Wireshark Wiki**: https://wiki.wireshark.org/S7comm
- **IPCOMM S7 Protocol**: https://www.ipcomm.de/protocol/S7ISOTCP/en/sheet.html

## Download Instructions

```bash
cd /mnt/c/ds/dsev2cpu/src/protocol/siemens/doc

# Download official manuals
wget -O S7-1500_Communication_Function_Manual.pdf "https://support.industry.siemens.com/cs/attachments/59192925/s71500_communication_function_manual_en-US_en-US.pdf"
wget -O S7_Communication_MN_s7-cps-ie_76.pdf "https://cache.industry.siemens.com/dl/files/198/30374198/att_63131/v1/MN_s7-cps-ie_76.pdf"
wget -O RFC1006_ISO_Transport_over_TCP.txt "https://www.rfc-editor.org/rfc/rfc1006.txt"
```
