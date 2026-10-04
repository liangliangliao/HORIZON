"""Verify preview identity, signing, native ABIs and Android media integration."""
from hashlib import sha256
from pathlib import Path
import struct
import sys
import zipfile


def length_prefixed(data, offset=0):
    length = struct.unpack_from('<I', data, offset)[0]
    end = offset + 4 + length
    if end > len(data):
        raise ValueError('Invalid signing structure length')
    return data[offset + 4:end], end


def certificate(apk):
    data = apk.read_bytes()
    eocd = data.rfind(b'PK\x05\x06')
    if eocd < 0:
        raise ValueError('Missing ZIP directory')
    directory = struct.unpack_from('<I', data, eocd + 16)[0]
    if data[directory - 16:directory] != b'APK Sig Block 42':
        raise ValueError('Missing APK signing block')
    size = struct.unpack_from('<Q', data, directory - 24)[0]
    start = directory - size - 8
    if struct.unpack_from('<Q', data, start)[0] != size:
        raise ValueError('Signing block sizes differ')
    offset = start + 8
    while offset < directory - 24:
        length, kind = struct.unpack_from('<QI', data, offset)
        if length < 4 or offset + 8 + length > directory - 24:
            raise ValueError('Invalid signing entry')
        if kind == 0x7109871A:
            signers, _ = length_prefixed(data[offset + 12:offset + 8 + length])
            signer, _ = length_prefixed(signers)
            signed, _ = length_prefixed(signer)
            _, next_field = length_prefixed(signed)
            certificates, _ = length_prefixed(signed, next_field)
            cert, _ = length_prefixed(certificates)
            return cert
        offset += 8 + length
    raise ValueError('Missing APK v2 certificate')


def verify(apk):
    expected = Path(__file__).resolve().parents[1] / 'tools/signing/horizon-preview.cer'
    assert certificate(apk) == expected.read_bytes(), f'{apk}: preview signer changed'
    with zipfile.ZipFile(apk) as archive:
        assert archive.testzip() is None, f'{apk}: corrupt ZIP entry'
        manifest = archive.read('AndroidManifest.xml')
        package = 'com.liangliangliao.horizon.preview'
        assert any(package.encode(e) in manifest for e in ('utf-8', 'utf-16le')), 'Incorrect preview identifier'
        for abi in ('armeabi-v7a', 'arm64-v8a'):
            for library in ('libunity.so', 'libil2cpp.so'):
                assert f'lib/{abi}/{library}' in archive.namelist(), f'Missing {abi}/{library}'
        dex_files = [archive.read(name) for name in archive.namelist()
                     if name.startswith('classes') and name.endswith('.dex')]
        assert dex_files, f'{apk}: missing DEX files'
        for name in ('TimelineEncoder', 'TimelineShareBridge', 'TimelineShareProvider'):
            descriptor = f'Lcom/horizon/media/{name};'.encode('ascii')
            assert any(descriptor in dex for dex in dex_files), f'{apk}: missing Android class {name}'
        provider = 'com.horizon.media.TimelineShareProvider'
        assert any(provider.encode(e) in manifest for e in ('utf-8', 'utf-16le')), 'Missing timeline share provider'
    print(f'{apk.name}: both ABIs, media DEX classes, preview identity, stable signer {sha256(certificate(apk)).hexdigest()} verified')


if __name__ == '__main__':
    location = Path(sys.argv[1] if len(sys.argv) > 1 else 'build')
    apks = [location] if location.is_file() else list(location.rglob('*.apk'))
    assert apks, 'No APK found'
    for apk in apks:
        verify(apk)
