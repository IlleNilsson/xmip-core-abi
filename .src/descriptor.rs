//! What a compiled module declares at load time, and whether the host takes it.
//!
//! The Rust side of `include/xmip_module.h` sections 1 and 4. The header is
//! normative (ADR-0012 clause 1); every name here follows it, less the `Xmip`
//! prefix the crate already supplies — `abi::XmipModuleDescriptor` would
//! say Xmip twice, `rust-style.md` section 5.
//!
//! This surface existed in the header from the start and not in this crate,
//! which is how `host.rs` in xmip-core-runtime imported three symbols nobody
//! had written and never compiled. ADR-0025 clause 5 closes that gap.
//!
//! Two questions, answered in one place. [`validate_module_abi`] asks whether a
//! descriptor is well formed at all; [`accepts`] asks the question a load
//! actually asks, which has a second party — the capability doing the loading
//! (ADR-0012, *Compatibility*). One well-formed descriptor is taken by
//! `contract` and refused by `transport`, and only the loader knows which of
//! the two asked for the library. Both live here, beside the descriptor they
//! judge, and the runtime's loader calls them.

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

/// What the loading capability requires of a module offering to serve it.
///
/// ADR-0012 clause 6: each core module versions its own trait, so the trait
/// version here is the loading capability's own and never a platform-wide
/// number. `contract` at 1.0 says nothing about `transport` at 1.0.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Expectation {
    /// The loading capability's name, as `descriptor.module` spells it:
    /// `contract`, `transport`, `message`, `path` (ADR-0011, the second of
    /// the three name parts).
    pub module: String,
    pub trait_major: u32,
    pub trait_minor: u32,
}

impl Expectation {
    #[must_use]
    pub fn new(module: &str, trait_major: u32, trait_minor: u32) -> Self {
        Self {
            module: module.to_string(),
            trait_major,
            trait_minor,
        }
    }
}

/// ADR-0012's compatibility rule, in its own order: `abi_version` equal,
/// `module` equal to the loading capability, `trait_major` equal,
/// `trait_minor` less than or equal (settled by the owner, 2026-09-26: a node
/// refuses a module newer in `trait_minor` than itself).
///
/// The first line is [`validate_module_abi`]'s, which also refuses a
/// descriptor that is not well formed; the other three need the expectation.
///
/// # Errors
///
/// Names the one field that disagreed and what was expected. An operator
/// reading this is holding a library file and has to know which fact about it
/// to fix — a module built for another capability, a trait generation apart,
/// or a table newer than the host can drive.
pub fn accepts(descriptor: &ModuleDescriptor, expected: &Expectation) -> Result<(), String> {
    validate_module_abi(descriptor)?;

    if descriptor.module != expected.module {
        return Err(format!(
            "{descriptor} answers the '{}' trait and '{}' is loading it: \
             descriptor.module must equal the loading capability",
            descriptor.module, expected.module
        ));
    }

    if descriptor.trait_major != expected.trait_major {
        return Err(format!(
            "{descriptor} is built against trait_major {} and '{}' speaks {}: \
             a major generation apart is a different trait",
            descriptor.trait_major, expected.module, expected.trait_major
        ));
    }

    if descriptor.trait_minor > expected.trait_minor {
        return Err(format!(
            "{descriptor} is built against trait_minor {} and '{}' speaks {}: \
             trait_minor must be less than or equal to the host's",
            descriptor.trait_minor, expected.module, expected.trait_minor
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

    fn contract_rust() -> ModuleDescriptor {
        ModuleDescriptor {
            abi_version: XMIP_ABI_VERSION,
            provider: "core".to_string(),
            module: "contract".to_string(),
            standard: "rust".to_string(),
            trait_major: 1,
            trait_minor: 0,
            module_major: 0,
            module_minor: 1,
            module_patch: 0,
        }
    }

    fn contract() -> Expectation {
        Expectation::new("contract", 1, 0)
    }

    #[test]
    fn the_capability_that_asked_takes_the_module_that_answers_it() {
        assert_eq!(accepts(&contract_rust(), &contract()), Ok(()));
    }

    #[test]
    fn a_foreign_abi_version_is_refused_before_the_capability_is_asked() {
        let mut foreign = contract_rust();
        foreign.abi_version = XMIP_ABI_VERSION + 1;

        let refusal = accepts(&foreign, &contract()).expect_err("must refuse");

        assert!(refusal.contains("ABI version 2"), "got: {refusal}");
    }

    #[test]
    fn another_capabilitys_module_is_refused_naming_both() {
        let mut transport = contract_rust();
        transport.module = "transport".to_string();

        let refusal = accepts(&transport, &contract()).expect_err("must refuse");

        assert!(refusal.contains("descriptor.module"), "got: {refusal}");
        assert!(refusal.contains("'transport' trait"), "got: {refusal}");
        assert!(
            refusal.contains("'contract' is loading it"),
            "got: {refusal}"
        );
    }

    #[test]
    fn a_major_generation_apart_is_refused_naming_trait_major() {
        let mut older = contract_rust();
        older.trait_major = 2;

        let refusal = accepts(&older, &contract()).expect_err("must refuse");

        assert!(refusal.contains("trait_major 2"), "got: {refusal}");
        assert!(refusal.contains("speaks 1"), "got: {refusal}");
    }

    #[test]
    fn a_newer_minor_is_refused_and_an_older_one_is_taken() {
        let mut newer = contract_rust();
        newer.trait_minor = 3;

        let refusal = accepts(&newer, &Expectation::new("contract", 1, 2))
            .expect_err("the host cannot drive a table it does not know");

        assert!(refusal.contains("trait_minor 3"), "got: {refusal}");
        assert!(refusal.contains("speaks 2"), "got: {refusal}");

        let mut older = contract_rust();
        older.trait_minor = 1;

        assert_eq!(accepts(&older, &Expectation::new("contract", 1, 4)), Ok(()));
    }
}
