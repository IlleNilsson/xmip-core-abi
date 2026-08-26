//! What a Module declares about itself, and how it is called.
//!
//! Arrived from the platform repository's `src/contracts.rs` on 2026-08-26.
//! `ModuleKind` did not come with it: ADR-0012 clause 5 removes it, and the
//! descriptor carries the module name as a string instead. Five kinds could not
//! describe seventeen traits, which is the whole reason that clause exists.

use serde::{Deserialize, Serialize};
use xmip_core::{JourneyId, MessageId};

/// Name and version. The kind is deliberately absent — see the module note.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ModuleIdentity {
    pub name: String,
    pub version: String,
}

/// What language or host the implementation actually runs in.
///
/// This is not a licence or a trust statement, only a loading fact: it tells
/// the host which loader to use and whether a bridge process is needed.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum ExecutionHostKind {
    NativeRust,
    DotNet,
    Java,
    Python,
    CAbi,
    Go,
    PowerShell,
    Bash,
}

/// One capability a Module claims, and the conditions it claims it under.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ModuleCapability {
    pub capability: String,
    pub execution_host: ExecutionHostKind,
    pub trusted_required: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ModuleManifest {
    pub identity: ModuleIdentity,
    pub capabilities: Vec<ModuleCapability>,
    pub entrypoint: ModuleEntrypoint,
}

/// Where the loader finds the code. A library and a symbol for an in-process
/// Module; an executable for one that runs beside the host.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ModuleEntrypoint {
    pub library_path: Option<String>,
    pub executable_path: Option<String>,
    pub symbol: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ExtensionManifest {
    pub name: String,
    pub version: String,
    pub execution_host: ExecutionHostKind,
    pub entrypoint: ExtensionEntrypoint,
    pub required_capabilities: Vec<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ExtensionEntrypoint {
    pub path: String,
    pub symbol_or_command: Option<String>,
}

/// One call across the boundary.
///
/// The payload crosses as a reference, never as bytes in this struct. A Stream
/// may be larger than memory, and the boundary rules in ADR-0012 make the host
/// responsible for handing out a reader rather than a buffer.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct HandlerInvocation {
    pub invocation_id: MessageId,
    pub journey_id: JourneyId,
    pub message_id: MessageId,
    pub artifact_name: String,
    pub location_name: Option<String>,
    pub payload_ref: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct HandlerResult {
    pub invocation_id: MessageId,
    pub status: HandlerStatus,
    pub output_payload_ref: Option<String>,
    pub promoted_properties: Vec<(String, String)>,
    pub diagnostic: Option<String>,
}

/// Whether the host may try again.
///
/// The Module decides this, not the host. Only the implementation knows whether
/// a refused connection is a restart away from working or a permanent answer.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum HandlerStatus {
    Completed,
    RetryableFailure,
    NonRetryableFailure,
}

/// The one thing every Module is.
///
/// Nothing else is declared here. ADR-0012 clause 6 gives each core module its
/// own trait, versioned on its own schedule, so `Transport` lives in the
/// transport module and not in the binding crate.
pub trait XmipModule: Send + Sync {
    fn manifest(&self) -> &ModuleManifest;
}

#[cfg(test)]
mod tests {
    use super::*;

    fn manifest() -> ModuleManifest {
        ModuleManifest {
            identity: ModuleIdentity {
                name: "xmip-core-transport-file".to_string(),
                version: "0.1.0".to_string(),
            },
            capabilities: vec![ModuleCapability {
                capability: "transport".to_string(),
                execution_host: ExecutionHostKind::NativeRust,
                trusted_required: false,
            }],
            entrypoint: ModuleEntrypoint {
                library_path: Some("libxmip_core_transport_file.so".to_string()),
                executable_path: None,
                symbol: Some("xmip_create_module_v1".to_string()),
            },
        }
    }

    #[test]
    fn a_manifest_round_trips_through_toml() {
        let original = manifest();
        let text = toml::to_string(&original).expect("serialize");
        let parsed: ModuleManifest = toml::from_str(&text).expect("deserialize");

        assert_eq!(original, parsed);
    }

    #[test]
    fn execution_host_is_kebab_case_on_the_wire() {
        let text = toml::to_string(&manifest()).expect("serialize");

        assert!(text.contains("native-rust"), "got: {text}");
    }

    #[test]
    fn a_module_may_claim_more_than_one_capability() {
        let mut manifest = manifest();
        manifest.capabilities.push(ModuleCapability {
            capability: "retain".to_string(),
            execution_host: ExecutionHostKind::NativeRust,
            trusted_required: true,
        });

        assert_eq!(manifest.capabilities.len(), 2);
    }

    #[test]
    fn a_retryable_failure_is_not_a_completion() {
        assert_ne!(HandlerStatus::RetryableFailure, HandlerStatus::Completed);
    }
}
