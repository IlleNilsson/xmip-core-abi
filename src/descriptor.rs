//! What a compiled module declares at load time, and whether the host takes it.
//!
//! The Rust side of `include/xmip_module.h` sections 1 and 4. The header is
//! normative (ADR-0012 clause 1); every name here follows it, less the `Xmip`
//! prefix the crate already supplies — `xmip_abi::XmipModuleDescriptor` would
//! say Xmip twice, `rust-style.md` section 5.
//!
//! This surface existed in the header from the start and not in this crate,
//! which is how `host.rs` in xmip-core-runtime imported three symbols nobody
//! had written and never compiled. ADR-0025 clause 5 closes that gap.

use std::fmt;

/// `XMIP_ABI_VERSION` in the header. The host refuses a module built against
/// any other, and the header puts the check at the entrypoint: a module that
/// cannot support the host's version returns `XMIP_E_UNSUPPORTED` there rather
/// than failing later.
pub const XMIP_ABI_VERSION: u32 = 1;

/// `XMIP_ENTRYPOINT` in the header: the one exported symbol,
/// `xmip_create_module_v1`. The version is in the name so that a second ABI can
/// coexist in one library during a migration.
pub const XMIP_ENTRYPOINT: &str = "xmip_create_module_v1";

/// What the module says it is. `XmipModuleDescriptor` in the header.
///
/// The three name parts are the same three parts as the repository name under
/// ADR-0011: the descriptor of `xmip-saxon-transform-xslt` reads
/// `provider = "saxon"`, `module = "transform"`, `standard = "xslt"`. The host
/// rejects a module whose descriptor disagrees with the artifact that asked
/// for it.
///
/// `trait_*` is the trait version the module was built against. `module_*` is
/// the module's own version and carries no compatibility meaning for the host.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ModuleDescriptor {
    pub abi_version: u32,
    pub provider: String,
    pub module: String,
    /// Empty only when the provider is `core`.
    pub standard: String,
    pub trait_major: u32,
    pub trait_minor: u32,
    pub module_major: u32,
    pub module_minor: u32,
    pub module_patch: u32,
}

impl fmt::Display for ModuleDescriptor {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "xmip-{}-{}", self.provider, self.module)?;

        if !self.standard.is_empty() {
            write!(f, "-{}", self.standard)?;
        }

        write!(
            f,
            " {}.{}.{} (abi {}, trait {}.{})",
            self.module_major,
            self.module_minor,
            self.module_patch,
            self.abi_version,
            self.trait_major,
            self.trait_minor
        )
    }
}

/// Whether the host accepts this descriptor. A load-time rejection, never a
/// cast — the header's words about the vtable, applied one step earlier.
///
/// # Errors
///
/// Names what disagrees and with what, because the operator reading the
/// refusal is holding a library file and needs to know which fact about it to
/// fix: an ABI built for another version, a nameless provider or module, or a
/// non-core provider claiming no standard.
pub fn validate_module_abi(descriptor: &ModuleDescriptor) -> Result<(), String> {
    if descriptor.abi_version != XMIP_ABI_VERSION {
        return Err(format!(
            "{descriptor} is built against ABI version {}, and this host speaks {XMIP_ABI_VERSION}",
            descriptor.abi_version
        ));
    }

    if descriptor.provider.trim().is_empty() || descriptor.module.trim().is_empty() {
        return Err(format!(
            "{descriptor} does not name its provider and module; ADR-0011 gives every module both"
        ));
    }

    if descriptor.standard.trim().is_empty() && descriptor.provider != "core" {
        return Err(format!(
            "{descriptor} names no standard, and only the core provider may omit one"
        ));
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    // The header is the normative statement (ADR-0012 clause 1), so the
    // constants are checked against it rather than against a copy of
    // themselves. A drift fails here, in the crate that drifted.
    const HEADER: &str = include_str!("../include/xmip_module.h");

    fn saxon_xslt() -> ModuleDescriptor {
        ModuleDescriptor {
            abi_version: XMIP_ABI_VERSION,
            provider: "saxon".to_string(),
            module: "transform".to_string(),
            standard: "xslt".to_string(),
            trait_major: 1,
            trait_minor: 0,
            module_major: 0,
            module_minor: 1,
            module_patch: 0,
        }
    }

    #[test]
    fn the_constants_are_the_headers_constants() {
        assert!(
            HEADER.contains(&format!("#define XMIP_ABI_VERSION  {XMIP_ABI_VERSION}u")),
            "the header no longer defines XMIP_ABI_VERSION as {XMIP_ABI_VERSION}"
        );
        assert!(
            HEADER.contains(&format!("\"{XMIP_ENTRYPOINT}\"")),
            "the header no longer names the entrypoint {XMIP_ENTRYPOINT}"
        );
    }

    #[test]
    fn a_matching_descriptor_is_accepted() {
        assert_eq!(validate_module_abi(&saxon_xslt()), Ok(()));
    }

    #[test]
    fn another_abi_version_is_refused_naming_both() {
        let mut descriptor = saxon_xslt();
        descriptor.abi_version = 2;

        let refusal = validate_module_abi(&descriptor).expect_err("must refuse");

        assert!(refusal.contains("version 2"), "got: {refusal}");
        assert!(refusal.contains("speaks 1"), "got: {refusal}");
    }

    #[test]
    fn only_the_core_provider_may_omit_the_standard() {
        let mut nameless = saxon_xslt();
        nameless.standard = String::new();

        validate_module_abi(&nameless).expect_err("a provider module names its standard");

        let mut core = saxon_xslt();
        core.provider = "core".to_string();
        core.standard = String::new();

        assert_eq!(validate_module_abi(&core), Ok(()));
    }

    #[test]
    fn the_descriptor_reads_as_the_repository_name() {
        // ADR-0011: the three name parts are the repository's three parts, so
        // the descriptor prints as the thing an operator would clone.
        assert!(
            saxon_xslt()
                .to_string()
                .starts_with("xmip-saxon-transform-xslt ")
        );
    }
}
